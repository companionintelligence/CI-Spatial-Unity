import hashlib
import importlib.util
import re
import sys
from pathlib import Path

import pytest

from app.config import Settings

_SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "fetch_weights.py"
_spec = importlib.util.spec_from_file_location("fetch_weights", _SCRIPT)
fetch_weights = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = fetch_weights
_spec.loader.exec_module(fetch_weights)


def _quiet(_message: str) -> None:
    pass


def _source(tmp_path: Path, name: str, payload: bytes) -> Path:
    source = tmp_path / "upstream" / name
    source.parent.mkdir(parents=True, exist_ok=True)
    source.write_bytes(payload)
    return source


def _artifact(payload: bytes, urls, **overrides):
    fields = dict(
        name="sample",
        env_var="CV_SERVER_TEST_SAMPLE_PATH",
        default_path="checkpoints/sample.bin",
        sha256=hashlib.sha256(payload).hexdigest(),
        size=len(payload),
        urls=tuple(urls),
    )
    fields.update(overrides)
    return fetch_weights.Artifact(**fields)


def _ensure(artifact, root, **kwargs):
    kwargs.setdefault("environ", {})
    kwargs.setdefault("log", _quiet)
    kwargs.setdefault("retry_delay", 0)
    return fetch_weights.ensure(artifact, root, **kwargs)


def _leftovers(directory: Path):
    return sorted(p.name for p in directory.glob("*.part"))


def test_downloads_and_verifies(tmp_path: Path) -> None:
    payload = b"weights" * 1000
    artifact = _artifact(payload, [_source(tmp_path, "w.bin", payload).as_uri()])
    root = tmp_path / "root"

    assert _ensure(artifact, root) is True

    target = root / "checkpoints" / "sample.bin"
    assert target.read_bytes() == payload
    assert _leftovers(target.parent) == []


def test_falls_back_to_the_next_url(tmp_path: Path) -> None:
    payload = b"fallback"
    missing = (tmp_path / "upstream" / "missing.bin").as_uri()
    artifact = _artifact(payload, [missing, _source(tmp_path, "w.bin", payload).as_uri()])

    assert _ensure(artifact, tmp_path / "root", attempts=2) is True
    assert (tmp_path / "root" / "checkpoints" / "sample.bin").read_bytes() == payload


def test_rejects_wrong_checksum_and_leaves_nothing(tmp_path: Path) -> None:
    source = _source(tmp_path, "w.bin", b"tampered")
    artifact = _artifact(b"original", [source.as_uri()], size=len(b"tampered"))
    root = tmp_path / "root"

    with pytest.raises(fetch_weights.FetchError, match="SHA-256"):
        _ensure(artifact, root)

    target_dir = root / "checkpoints"
    assert not (target_dir / "sample.bin").exists()
    assert _leftovers(target_dir) == []


@pytest.mark.parametrize("served", [b"too long payload", b"short"])
def test_rejects_wrong_size(tmp_path: Path, served: bytes) -> None:
    expected = b"payload!"
    artifact = _artifact(expected, [_source(tmp_path, "w.bin", served).as_uri()])
    root = tmp_path / "root"

    with pytest.raises(fetch_weights.FetchError):
        _ensure(artifact, root, attempts=1)
    assert not (root / "checkpoints" / "sample.bin").exists()
    assert _leftovers(root / "checkpoints") == []


def test_keeps_a_verified_file_without_downloading(tmp_path: Path) -> None:
    payload = b"already here"
    artifact = _artifact(payload, [(tmp_path / "nowhere.bin").as_uri()])
    target = tmp_path / "root" / "checkpoints" / "sample.bin"
    target.parent.mkdir(parents=True)
    target.write_bytes(payload)

    assert _ensure(artifact, tmp_path / "root") is True
    assert target.read_bytes() == payload


def test_does_not_overwrite_a_mismatching_file(tmp_path: Path) -> None:
    payload = b"pinned"
    artifact = _artifact(payload, [_source(tmp_path, "w.bin", payload).as_uri()])
    target = tmp_path / "root" / "checkpoints" / "sample.bin"
    target.parent.mkdir(parents=True)
    target.write_bytes(b"someone else's file")

    with pytest.raises(fetch_weights.FetchError, match="--force"):
        _ensure(artifact, tmp_path / "root")
    assert target.read_bytes() == b"someone else's file"

    assert _ensure(artifact, tmp_path / "root", force=True) is True
    assert target.read_bytes() == payload


def test_check_mode_never_downloads(tmp_path: Path) -> None:
    payload = b"pinned"
    artifact = _artifact(payload, [_source(tmp_path, "w.bin", payload).as_uri()])
    root = tmp_path / "root"
    target = root / "checkpoints" / "sample.bin"

    assert _ensure(artifact, root, check_only=True) is False
    assert not target.exists()

    target.parent.mkdir(parents=True)
    target.write_bytes(b"different")
    assert _ensure(artifact, root, check_only=True) is False
    assert target.read_bytes() == b"different"


def test_env_var_overrides_the_destination(tmp_path: Path) -> None:
    payload = b"custom location"
    artifact = _artifact(payload, [_source(tmp_path, "w.bin", payload).as_uri()])
    custom = tmp_path / "elsewhere" / "model.bin"

    assert _ensure(artifact, tmp_path / "root", environ={artifact.env_var: str(custom)}) is True
    assert custom.read_bytes() == payload
    assert not (tmp_path / "root" / "checkpoints" / "sample.bin").exists()


def test_main_exit_codes(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    payload = b"main"
    artifact = _artifact(payload, [_source(tmp_path, "w.bin", payload).as_uri()])
    monkeypatch.setattr(fetch_weights, "MANIFEST", (artifact,))
    monkeypatch.delenv(artifact.env_var, raising=False)
    root = str(tmp_path / "root")

    assert fetch_weights.main(["--root", root, "--check"]) == 1
    assert fetch_weights.main(["--root", root]) == 0
    assert fetch_weights.main(["--root", root, "--check"]) == 0

    broken = _artifact(b"other", [(tmp_path / "nowhere.bin").as_uri()], name="broken")
    monkeypatch.setattr(fetch_weights, "MANIFEST", (broken,))
    assert fetch_weights.main(["--root", root, "--attempts", "1"]) == 1


def test_manifest_urls_and_checksums_are_pinned() -> None:
    names = [artifact.name for artifact in fetch_weights.MANIFEST]
    assert len(names) == len(set(names))
    for artifact in fetch_weights.MANIFEST:
        assert re.fullmatch(r"[0-9a-f]{64}", artifact.sha256), artifact.name
        assert artifact.size > 0
        assert artifact.urls, artifact.name
        for url in artifact.urls:
            assert url.startswith("https://"), url
            if url.startswith("https://huggingface.co/"):
                assert re.search(r"/resolve/[0-9a-f]{40}/", url), url
            elif url.startswith("https://raw.githubusercontent.com/"):
                assert re.search(r"/[0-9a-f]{40}/", url), url
            elif url.startswith("https://github.com/"):
                assert "/releases/download/" in url, url


def test_manifest_targets_match_the_server_defaults() -> None:
    for artifact in fetch_weights.MANIFEST:
        field = Settings.model_fields[artifact.env_var.lower()]
        assert field.default == artifact.default_path, artifact.name
