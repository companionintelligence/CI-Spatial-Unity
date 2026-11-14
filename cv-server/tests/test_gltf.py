"""glTF uploads: opaque IDs, DELETE /v1/gltf/{id} and GLTF_UPLOAD_RETENTION_DAYS."""

import asyncio
import os
import re
import time
from pathlib import Path
from typing import List, Optional, Sequence
from urllib.parse import quote

import pytest
from fastapi.testclient import TestClient
from pydantic import SecretStr, ValidationError

from app import gltf_store
from app.config import Settings, settings
from app.main import app
from app.model_manager import ModelManager

client = TestClient(app)

UPLOAD_ID = re.compile("[0-9a-f]{32}")
DAY = 24 * 60 * 60
GLTF = b'{"asset": {"version": "2.0"}}'
ALL_SUFFIXES = (".gltf", ".bin", ".extras.json")
# An upload stored before opaque IDs, named the way the server used to name them.
LEGACY_ID = "spatial-map-20250102-030405-0a1b2c3d"


@pytest.fixture
def upload_dir(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> Path:
    directory = tmp_path / "uploads"
    monkeypatch.setattr(settings, "gltf_upload_dir", str(directory))
    return directory


def upload(
    headers: dict,
    *,
    bin_data: Optional[bytes] = None,
    extras: Optional[str] = None,
    filename: str = "scan.gltf",
) -> dict:
    files = {"gltf": (filename, GLTF, "model/gltf+json")}
    if bin_data is not None:
        files["bin"] = ("scan.bin", bin_data, "application/octet-stream")
    data = {"extras": extras} if extras is not None else None
    response = client.post("/v1/gltf", files=files, data=data, headers=headers)
    assert response.status_code == 200, response.text
    return response.json()


def stored_upload(
    directory: Path, upload_id: str, age_days: float, suffixes: Sequence[str] = ALL_SUFFIXES
) -> List[Path]:
    """Write an upload's files straight to disk, last modified ``age_days`` ago."""
    directory.mkdir(parents=True, exist_ok=True)
    stamp = time.time() - age_days * DAY
    paths = []
    for suffix in suffixes:
        path = directory / f"{upload_id}{suffix}"
        path.write_bytes(b"{}")
        os.utime(path, (stamp, stamp))
        paths.append(path)
    return paths


# --- IDs ------------------------------------------------------------------------------


def test_upload_is_stored_under_an_opaque_random_id(auth_headers: dict, upload_dir: Path) -> None:
    # The Quest app names its export after the room's anchor UUID; none of it is kept.
    first = upload(
        auth_headers,
        bin_data=b"\x00\x01",
        extras='{"pins": []}',
        filename="spatial-map_6f1c2a5e-room.gltf",
    )
    second = upload(auth_headers)

    for body in (first, second):
        assert body["ok"] is True
        assert UPLOAD_ID.fullmatch(body["id"])
    assert first["id"] != second["id"]

    assert sorted(path.name for path in upload_dir.iterdir()) == sorted(
        [first["id"] + suffix for suffix in ALL_SUFFIXES] + [second["id"] + ".gltf"]
    )
    assert first["gltf_path"] == os.path.join(str(upload_dir), first["id"] + ".gltf")
    assert first["bin_path"] == os.path.join(str(upload_dir), first["id"] + ".bin")
    assert first["extras_path"] == os.path.join(str(upload_dir), first["id"] + ".extras.json")
    assert second["bin_path"] is None
    assert second["extras_path"] is None
    assert (upload_dir / f"{first['id']}.gltf").read_bytes() == GLTF
    assert (upload_dir / f"{first['id']}.bin").read_bytes() == b"\x00\x01"
    assert (upload_dir / f"{first['id']}.extras.json").read_text(encoding="utf-8") == '{"pins": []}'


def test_upload_ids_are_random_not_counters_or_timestamps() -> None:
    ids = [gltf_store.new_upload_id() for _ in range(2000)]
    assert all(UPLOAD_ID.fullmatch(upload_id) for upload_id in ids)
    # IDs from a clock or a counter share long prefixes when made in a row. With 48
    # random bits, two of 2000 IDs share a 12-character prefix about once in 10^8 runs.
    assert len({upload_id[:12] for upload_id in ids}) == len(ids)


@pytest.mark.parametrize("value", ["0123456789abcdef0123456789abcdef", LEGACY_ID])
def test_upload_id_accepts_new_and_earlier_ids(value: str) -> None:
    assert gltf_store.is_upload_id(value)


@pytest.mark.parametrize(
    "value",
    [
        "",
        "0123456789abcdef",
        "0123456789abcdef0123456789abcdef0",
        "0123456789ABCDEF0123456789ABCDEF",
        "0123456789abcdef0123456789abcdeg",
        "0123456789abcdef0123456789abcdef\n",
        "0123456789abcdef0123456789abcdef.gltf",
        "..\\0123456789abcdef0123456789abcd",
        "spatial-map",
        LEGACY_ID + ".gltf",
        LEGACY_ID.upper(),
        "spatial-map-2025010-030405-0a1b2c3d",
        # Digits outside ASCII match \d, but not an upload name.
        "spatial-map-\u0662\u0660\u0662\u0665\u0660\u0661\u0660\u0662-030405-0a1b2c3d",
    ],
)
def test_upload_id_rejects_anything_else(value: str) -> None:
    assert not gltf_store.is_upload_id(value)


def test_an_id_already_on_disk_is_never_reused(
    monkeypatch: pytest.MonkeyPatch, upload_dir: Path
) -> None:
    taken = gltf_store.new_upload_id()
    (existing,) = stored_upload(upload_dir, taken, age_days=0, suffixes=(".gltf",))
    fresh = gltf_store.new_upload_id()
    ids = iter([taken, fresh])
    monkeypatch.setattr(gltf_store, "new_upload_id", lambda: next(ids))

    stored = gltf_store.save_upload(str(upload_dir), GLTF)

    assert stored.upload_id == fresh
    assert existing.read_bytes() == b"{}"


def test_a_failed_upload_leaves_no_files(upload_dir: Path) -> None:
    # A lone surrogate cannot be encoded, so writing extras fails after the glTF and bin.
    with pytest.raises(UnicodeEncodeError):
        gltf_store.save_upload(str(upload_dir), GLTF, b"\x00", "\ud800")
    assert list(upload_dir.iterdir()) == []


# --- DELETE /v1/gltf/{id} ---------------------------------------------------------------


def test_delete_removes_every_file_of_the_upload(auth_headers: dict, upload_dir: Path) -> None:
    doomed = upload(auth_headers, bin_data=b"\x00", extras="{}")
    kept = upload(auth_headers)

    response = client.delete(f"/v1/gltf/{doomed['id']}", headers=auth_headers)
    assert response.status_code == 204
    assert response.content == b""
    assert [path.name for path in upload_dir.iterdir()] == [kept["id"] + ".gltf"]

    # Gone, so the ID is unknown now.
    response = client.delete(f"/v1/gltf/{doomed['id']}", headers=auth_headers)
    assert response.status_code == 404


@pytest.mark.parametrize(
    "upload_id",
    ["0123456789abcdef0123456789abcdef", LEGACY_ID, "not-an-upload", "scan.gltf"],
)
def test_delete_of_an_unknown_id_is_404(
    upload_id: str, auth_headers: dict, upload_dir: Path
) -> None:
    kept = upload(auth_headers)
    response = client.delete(f"/v1/gltf/{upload_id}", headers=auth_headers)
    assert response.status_code == 404
    assert response.json() == {"detail": "Upload not found."}
    assert (upload_dir / f"{kept['id']}.gltf").exists()


def test_delete_needs_the_exact_id(auth_headers: dict, upload_dir: Path) -> None:
    kept = upload(auth_headers)
    upload_id = kept["id"]
    for variant in (upload_id.upper(), upload_id + ".gltf", upload_id[:-1], upload_id + "\n"):
        response = client.delete("/v1/gltf/" + quote(variant), headers=auth_headers)
        assert response.status_code == 404, variant
    assert (upload_dir / f"{upload_id}.gltf").exists()


@pytest.mark.parametrize(
    "path",
    ["/v1/gltf/..%5Cvictim", "/v1/gltf/%2E%2E%5Cvictim", "/v1/gltf/..%2Fvictim"],
)
def test_delete_stays_inside_the_upload_dir(
    path: str, auth_headers: dict, upload_dir: Path, tmp_path: Path
) -> None:
    # On Windows a backslash separates directories, so "..\victim" would leave the folder.
    upload_dir.mkdir()
    victim = tmp_path / "victim.gltf"
    victim.write_bytes(GLTF)
    assert client.delete(path, headers=auth_headers).status_code == 404
    assert victim.exists()


def test_delete_leaves_files_that_are_not_uploads(auth_headers: dict, upload_dir: Path) -> None:
    # These files exist and have upload suffixes, but their names are not upload IDs, so
    # only the ID check keeps DELETE away from them. Unlike the case and backslash checks,
    # this fails on every file system when that check is missing.
    upload_dir.mkdir()
    files = {
        "scan": "scan.gltf",
        "notes": "notes.bin",
        "x": "x.extras.json",
        "spatial-map": "spatial-map.gltf",
        "0123456789ABCDEF0123456789ABCDEF": "0123456789ABCDEF0123456789ABCDEF.gltf",
    }
    for name in files.values():
        (upload_dir / name).write_bytes(GLTF)

    for name in files:
        response = client.delete(f"/v1/gltf/{name}", headers=auth_headers)
        assert response.status_code == 404, name
        assert response.json() == {"detail": "Upload not found."}, name
    assert sorted(path.name for path in upload_dir.iterdir()) == sorted(files.values())
    assert all((upload_dir / name).read_bytes() == GLTF for name in files.values())


def test_uploads_from_before_opaque_ids_stay_readable_and_can_be_deleted(
    auth_headers: dict, upload_dir: Path
) -> None:
    legacy = stored_upload(upload_dir, LEGACY_ID, age_days=400)
    before = {path: (path.read_bytes(), path.stat().st_mtime) for path in legacy}

    new = upload(auth_headers, bin_data=b"\x00", extras="{}")
    assert {path: (path.read_bytes(), path.stat().st_mtime) for path in legacy} == before

    response = client.delete(f"/v1/gltf/{LEGACY_ID}", headers=auth_headers)
    assert response.status_code == 204
    assert not any(path.exists() for path in legacy)
    assert all((upload_dir / f"{new['id']}{suffix}").exists() for suffix in ALL_SUFFIXES)


@pytest.mark.parametrize(
    "authorization",
    [None, "Bearer wrong-token", "Basic dXNlcjpwYXNz", "Bearer"],
)
def test_delete_needs_the_bearer_token(
    authorization: Optional[str], auth_headers: dict, upload_dir: Path
) -> None:
    kept = upload(auth_headers)
    headers = {"Authorization": authorization} if authorization is not None else {}
    response = client.delete(f"/v1/gltf/{kept['id']}", headers=headers)
    assert response.status_code == 401
    assert response.headers["www-authenticate"].startswith("Bearer")
    assert (upload_dir / f"{kept['id']}.gltf").exists()


def test_without_the_token_unknown_and_known_ids_look_the_same(
    auth_headers: dict, upload_dir: Path
) -> None:
    kept = upload(auth_headers)
    known = client.delete(f"/v1/gltf/{kept['id']}")
    unknown = client.delete("/v1/gltf/0123456789abcdef0123456789abcdef")
    assert known.status_code == unknown.status_code == 401
    assert known.json() == unknown.json()


def test_delete_fails_closed_without_a_configured_token(
    auth_headers: dict, monkeypatch: pytest.MonkeyPatch, upload_dir: Path
) -> None:
    kept = upload(auth_headers)
    monkeypatch.setattr(settings, "api_token", SecretStr(""))
    response = client.delete(f"/v1/gltf/{kept['id']}", headers={"Authorization": "Bearer "})
    assert response.status_code == 503
    assert (upload_dir / f"{kept['id']}.gltf").exists()


# --- Retention ---------------------------------------------------------------------------


def test_retention_is_off_by_default(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("GLTF_UPLOAD_RETENTION_DAYS", raising=False)
    assert Settings().gltf_upload_retention_days == 0


@pytest.mark.parametrize("raw,days", [("30", 30), (" 7 ", 7), ("0", 0), ("", 0)])
def test_retention_days_come_from_the_environment(
    raw: str, days: int, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setenv("GLTF_UPLOAD_RETENTION_DAYS", raw)
    assert Settings().gltf_upload_retention_days == days


@pytest.mark.parametrize("raw", ["-1", "1.5", "a week"])
def test_invalid_retention_days_are_refused(raw: str, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("GLTF_UPLOAD_RETENTION_DAYS", raw)
    with pytest.raises(ValidationError):
        Settings()


def test_retention_off_keeps_every_upload(upload_dir: Path) -> None:
    files = stored_upload(upload_dir, gltf_store.new_upload_id(), age_days=3650)
    files += stored_upload(upload_dir, LEGACY_ID, age_days=3650)
    assert gltf_store.delete_expired_uploads(str(upload_dir), 0) == []
    assert all(path.exists() for path in files)


def test_retention_deletes_only_uploads_older_than_the_period(upload_dir: Path) -> None:
    expired = gltf_store.new_upload_id()
    expired_files = stored_upload(upload_dir, expired, age_days=8)
    expired_files += stored_upload(upload_dir, LEGACY_ID, age_days=30)
    recent_files = stored_upload(upload_dir, gltf_store.new_upload_id(), age_days=6)
    # Old, but not named like an upload: never deleted.
    month_ago = time.time() - 30 * DAY
    others = []
    for name in ("notes.txt", "scan.gltf", f"{expired}.glb", "spatial-map.gltf"):
        path = upload_dir / name
        path.write_bytes(b"{}")
        os.utime(path, (month_ago, month_ago))
        others.append(path)
    folder = upload_dir / f"{gltf_store.new_upload_id()}.gltf"
    folder.mkdir()
    os.utime(folder, (month_ago, month_ago))

    deleted = gltf_store.delete_expired_uploads(str(upload_dir), 7)

    assert sorted(deleted) == sorted([expired, LEGACY_ID])
    assert not any(path.exists() for path in expired_files)
    assert all(path.exists() for path in recent_files + others + [folder])


def test_an_upload_expires_with_its_newest_file(upload_dir: Path) -> None:
    upload_id = gltf_store.new_upload_id()
    files = stored_upload(upload_dir, upload_id, age_days=10, suffixes=(".gltf", ".bin"))
    files += stored_upload(upload_dir, upload_id, age_days=2, suffixes=(".extras.json",))
    assert gltf_store.delete_expired_uploads(str(upload_dir), 7) == []
    assert all(path.exists() for path in files)
    assert gltf_store.delete_expired_uploads(str(upload_dir), 1) == [upload_id]
    assert not any(path.exists() for path in files)


def test_retention_before_the_first_upload(tmp_path: Path) -> None:
    assert gltf_store.delete_expired_uploads(str(tmp_path / "uploads"), 7) == []


@pytest.mark.parametrize("days,old_upload_kept", [(0, True), (7, False)])
def test_server_start_applies_the_retention_period(
    days: int, old_upload_kept: bool, monkeypatch: pytest.MonkeyPatch, upload_dir: Path
) -> None:
    monkeypatch.setattr(settings, "gltf_upload_retention_days", days)
    monkeypatch.setattr(settings, "load_all_models", False)
    monkeypatch.setattr(ModelManager, "_instance", None)
    old = stored_upload(upload_dir, gltf_store.new_upload_id(), age_days=30)
    recent = stored_upload(upload_dir, gltf_store.new_upload_id(), age_days=1)

    with TestClient(app):  # runs the startup handlers, and the shutdown handlers on exit
        assert all(path.exists() == old_upload_kept for path in old)
        assert all(path.exists() for path in recent)
        assert (gltf_store._retention_task is not None) == (days > 0)
    assert gltf_store._retention_task is None


def test_retention_runs_again_while_the_server_is_up(monkeypatch: pytest.MonkeyPatch) -> None:
    sweeps = []

    def sweep() -> list:
        sweeps.append(1)
        if len(sweeps) == 1:
            raise PermissionError("a failed sweep must not stop the next one")
        return []

    monkeypatch.setattr(gltf_store, "delete_expired_uploads_now", sweep)

    async def three_sweeps() -> None:
        task = asyncio.create_task(gltf_store.retention_loop(interval_seconds=0.01))
        while len(sweeps) < 3:
            await asyncio.sleep(0.01)
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task

    asyncio.run(asyncio.wait_for(three_sweeps(), timeout=10))
