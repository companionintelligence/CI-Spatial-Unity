"""Hugging Face downloads are pinned to commits (app/hub_pins.py); no network or weights needed."""

import contextlib
from pathlib import Path
from types import SimpleNamespace

import pytest

from app import hub_pins
from app import model_manager as mm_module
from app.model_manager import ModelManager

COMMIT = "0123456789abcdef0123456789abcdef01234567"
LARGE = "florence-community/Florence-2-large"
MS_LARGE = "microsoft/Florence-2-large"


def test_pins_are_full_commit_ids() -> None:
    assert hub_pins.is_commit_id(hub_pins.BERT_BASE_UNCASED.revision)
    assert hub_pins.FLORENCE2_REVISIONS
    for revision in hub_pins.FLORENCE2_REVISIONS.values():
        assert hub_pins.is_commit_id(revision)


def test_default_florence_model_has_a_pin() -> None:
    from app.config import Settings

    default_id = Settings.model_fields["florence_model_id"].default
    assert hub_pins.florence_model(default_id) == (LARGE, hub_pins.FLORENCE2_REVISIONS[LARGE])


def test_pin_lookup_ignores_case() -> None:
    assert hub_pins.florence_revision(LARGE.lower()) == hub_pins.FLORENCE2_REVISIONS[LARGE]


def test_florence_model_revision_overrides_the_pin() -> None:
    assert hub_pins.florence_revision(LARGE, COMMIT) == COMMIT
    assert hub_pins.florence_revision(LARGE, f"  {COMMIT}\n") == COMMIT


def test_unknown_model_needs_a_revision() -> None:
    with pytest.raises(RuntimeError, match="FLORENCE_MODEL_REVISION"):
        hub_pins.florence_revision("someone/florence-2-finetune")
    assert hub_pins.florence_revision("someone/florence-2-finetune", COMMIT) == COMMIT


@pytest.mark.parametrize("revision", ["main", "v1.0", "refs/pr/1", COMMIT[:7], COMMIT.upper(), COMMIT + "0"])
def test_moving_or_short_revisions_are_refused(revision: str) -> None:
    with pytest.raises(RuntimeError, match="40-character commit id"):
        hub_pins.florence_revision(LARGE, revision)


def test_local_directory_needs_no_revision(tmp_path: Path) -> None:
    assert hub_pins.florence_revision(str(tmp_path)) is None
    assert hub_pins.florence_revision(str(tmp_path), COMMIT) == COMMIT
    assert hub_pins.florence_model(str(tmp_path)) == (str(tmp_path), None)


@pytest.mark.parametrize(
    "model_id, repo",
    [
        (MS_LARGE, LARGE),
        (MS_LARGE.lower(), LARGE),
        ("microsoft/Florence-2-base", "florence-community/Florence-2-base"),
    ],
)
def test_microsoft_ids_load_the_converted_repository(model_id: str, repo: str) -> None:
    assert hub_pins.florence_model(model_id) == (repo, hub_pins.FLORENCE2_REVISIONS[repo])


def test_microsoft_id_with_a_commit_is_refused() -> None:
    with pytest.raises(RuntimeError, match="florence-community/Florence-2-large"):
        hub_pins.florence_model(MS_LARGE, COMMIT)


class _FakeLoader:
    def __init__(self, result, calls: list) -> None:
        self.result = result
        self.calls = calls

    def from_pretrained(self, model_id, **kwargs):
        self.calls.append((model_id, kwargs))
        return self.result


def _fake_florence(
    monkeypatch: pytest.MonkeyPatch, model_id: str, revision: str, missing_keys=()
) -> list:
    calls: list = []
    model = SimpleNamespace(to=lambda device: model)
    loading_info = {"missing_keys": set(missing_keys), "unexpected_keys": set()}
    monkeypatch.setattr(mm_module, "Florence2ForConditionalGeneration", _FakeLoader((model, loading_info), calls))
    monkeypatch.setattr(mm_module, "Florence2Processor", _FakeLoader(object(), calls))
    monkeypatch.setattr(mm_module.settings, "florence_model_id", model_id)
    monkeypatch.setattr(mm_module.settings, "florence_model_revision", revision)
    return calls


@pytest.mark.parametrize("model_id", [LARGE, MS_LARGE])
def test_florence_model_and_processor_load_the_pinned_commit(monkeypatch: pytest.MonkeyPatch, model_id: str) -> None:
    calls = _fake_florence(monkeypatch, model_id, "")

    manager = ModelManager()
    manager.load_florence2()

    assert [repo for repo, _ in calls] == [LARGE, LARGE]
    for _, kwargs in calls:
        assert kwargs["revision"] == hub_pins.FLORENCE2_REVISIONS[LARGE]
        # transformers' own implementation: nothing from the repository is run as code.
        assert "trust_remote_code" not in kwargs
    assert calls[0][1]["output_loading_info"] is True
    assert manager.florence_model is not None and manager.florence_processor is not None


def test_florence_checkpoint_with_missing_weights_is_refused(monkeypatch: pytest.MonkeyPatch) -> None:
    _fake_florence(monkeypatch, "someone/florence-2-remote-code", COMMIT, missing_keys={"model.weight"})

    manager = ModelManager()
    with pytest.raises(RuntimeError, match="1 weights are missing"):
        manager.load_florence2()
    assert manager.florence_model is None


def test_unpinned_florence_model_is_not_downloaded(monkeypatch: pytest.MonkeyPatch) -> None:
    calls = _fake_florence(monkeypatch, "someone/florence-2-finetune", "")

    with pytest.raises(RuntimeError, match="FLORENCE_MODEL_REVISION"):
        ModelManager().load_florence2()
    assert calls == []


def _fake_grounding_dino(monkeypatch: pytest.MonkeyPatch, tmp_path: Path, missing_keys=()) -> tuple:
    downloads: list = []
    built: dict = {"encoder_swapped": False}

    def fake_snapshot_download(repo_id, **kwargs):
        downloads.append((repo_id, kwargs))
        return str(tmp_path)

    class FakeModel:
        def load_state_dict(self, state, strict):
            built["state"], built["strict"] = state, strict
            return SimpleNamespace(missing_keys=list(missing_keys), unexpected_keys=["label_enc.weight"])

        def eval(self):
            built["eval"] = True
            return self

    @contextlib.contextmanager
    def fake_text_encoder_swap():
        built["encoder_swapped"] = True
        yield
        built["encoder_swapped"] = False

    def fake_build(args):
        built["text_encoder_type"] = args.text_encoder_type
        built["built_with_swap"] = built["encoder_swapped"]
        return FakeModel()

    def fake_torch_load(path, **kwargs):
        built["load_kwargs"] = kwargs
        return {"model": {"module.weight": 1}}

    monkeypatch.setattr(mm_module, "snapshot_download", fake_snapshot_download)
    monkeypatch.setattr(mm_module, "SLConfig", SimpleNamespace(fromfile=lambda path: SimpleNamespace()))
    monkeypatch.setattr(mm_module, "build_gdino_model", fake_build)
    monkeypatch.setattr(mm_module, "bert_text_encoder_for_groundingdino", fake_text_encoder_swap)
    monkeypatch.setattr(mm_module, "clean_state_dict", lambda state: {"weight": state["module.weight"]})
    monkeypatch.setattr(mm_module.torch, "load", fake_torch_load)
    return downloads, built, FakeModel


def test_grounding_dino_text_encoder_is_the_pinned_bert(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    downloads, built, fake_model = _fake_grounding_dino(monkeypatch, tmp_path)

    manager = ModelManager()
    manager.load_grounding_dino()

    assert downloads == [
        (
            hub_pins.BERT_BASE_UNCASED.repo_id,
            {
                "revision": hub_pins.BERT_BASE_UNCASED.revision,
                "allow_patterns": list(hub_pins.BERT_FILES),
            },
        )
    ]
    assert built["text_encoder_type"] == str(tmp_path)
    assert built["built_with_swap"] is True and built["encoder_swapped"] is False
    assert built["state"] == {"weight": 1} and built["strict"] is False and built["eval"]
    assert built["load_kwargs"]["weights_only"] is True
    assert isinstance(manager.gdino_model, fake_model)


def test_grounding_dino_checkpoint_must_cover_every_weight(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    _fake_grounding_dino(monkeypatch, tmp_path, missing_keys=["bert.encoder.layer.0.attention.self.query.weight"])

    manager = ModelManager()
    with pytest.raises(RuntimeError, match="lacks 1 of the model's weights"):
        manager.load_grounding_dino()
    assert manager.gdino_model is None
