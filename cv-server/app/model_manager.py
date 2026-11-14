from __future__ import annotations

from typing import Optional

import torch

from app import hub_pins
from app.config import settings
from app.gdino_text_encoder import bert_text_encoder_for_groundingdino

_gdino_import_error = None
try:
    from groundingdino.models import build_model as build_gdino_model
    from groundingdino.util.misc import clean_state_dict
    from groundingdino.util.slconfig import SLConfig
except Exception as exc:  # pragma: no cover - optional import until deps are installed
    build_gdino_model = None
    clean_state_dict = None
    SLConfig = None
    _gdino_import_error = exc

try:
    from huggingface_hub import snapshot_download
except Exception:  # pragma: no cover
    snapshot_download = None

_sam2_import_error = None
try:
    from sam2.build_sam import build_sam2
    from sam2.sam2_image_predictor import SAM2ImagePredictor
except Exception as exc:  # pragma: no cover
    build_sam2 = None
    SAM2ImagePredictor = None
    _sam2_import_error = exc

try:
    from transformers import Florence2ForConditionalGeneration, Florence2Processor
except Exception:  # pragma: no cover
    Florence2ForConditionalGeneration = None
    Florence2Processor = None


class ModelManager:
    _instance: Optional["ModelManager"] = None

    def __init__(self) -> None:
        self.device = torch.device(settings.device if torch.cuda.is_available() else "cpu")
        self.gdino_model = None
        self.sam2_predictor = None
        self.florence_model = None
        self.florence_processor = None

    @classmethod
    def get_instance(cls) -> "ModelManager":
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def load_grounding_dino(self) -> None:
        if self.gdino_model is not None:
            return
        if build_gdino_model is None or snapshot_download is None:
            detail = f"{type(_gdino_import_error).__name__}: {_gdino_import_error}" if _gdino_import_error else "Unknown import error"
            raise RuntimeError(f"GroundingDINO dependencies are not available. {detail}")
        # groundingdino.util.inference.load_model, except for the text encoder: the config
        # names "bert-base-uncased", which GroundingDINO would download from the hub's
        # moving main branch. Point it at a local snapshot of the pinned commit instead.
        args = SLConfig.fromfile(settings.gdino_config_path)
        args.device = str(self.device)
        args.text_encoder_type = snapshot_download(
            hub_pins.BERT_BASE_UNCASED.repo_id,
            revision=hub_pins.BERT_BASE_UNCASED.revision,
            allow_patterns=list(hub_pins.BERT_FILES),
        )
        # GroundingDINO's own BERT wrapper does not run on transformers 5 (app/gdino_text_encoder.py).
        with bert_text_encoder_for_groundingdino():
            model = build_gdino_model(args)
        # The checkpoint holds only tensors, so the restricted unpickler can read it.
        checkpoint = torch.load(settings.gdino_weights_path, map_location="cpu", weights_only=True)
        # Not strict, as in GroundingDINO's loader: the checkpoint also holds weights the
        # model does not use. Every weight the model has must come from it, though; a
        # renamed parameter would otherwise keep its initial value without an error.
        result = model.load_state_dict(clean_state_dict(checkpoint["model"]), strict=False)
        if result.missing_keys:
            raise RuntimeError(
                f"The GroundingDINO checkpoint {settings.gdino_weights_path} lacks "
                f"{len(result.missing_keys)} of the model's weights, e.g. {sorted(result.missing_keys)[:3]}"
            )
        model.eval()
        self.gdino_model = model

    def load_sam2(self) -> None:
        if self.sam2_predictor is not None:
            return
        if build_sam2 is None or SAM2ImagePredictor is None:
            detail = f"{type(_sam2_import_error).__name__}: {_sam2_import_error}" if _sam2_import_error else "Unknown import error"
            raise RuntimeError(f"SAM2 dependencies are not available. {detail}")
        sam2_model = build_sam2(
            settings.sam2_config_path,
            settings.sam2_weights_path,
            device=str(self.device),
        )
        self.sam2_predictor = SAM2ImagePredictor(sam2_model)

    def load_florence2(self) -> None:
        if self.florence_model is not None:
            return
        if Florence2ForConditionalGeneration is None or Florence2Processor is None:
            raise RuntimeError("Transformers dependencies are not available.")
        # transformers' own Florence-2 implementation: the repository supplies weights,
        # configs and tokenizer files but no code, all from one fixed commit
        # (app/hub_pins.py, or FLORENCE_MODEL_REVISION).
        model_id, revision = hub_pins.florence_model(
            settings.florence_model_id, settings.florence_model_revision
        )
        model, loading_info = Florence2ForConditionalGeneration.from_pretrained(
            model_id,
            revision=revision,
            dtype=torch.float16,
            output_loading_info=True,
        )
        # A repository made for Florence-2's own repository code (such as Microsoft's) has
        # other weight names; transformers would leave those weights at random values.
        missing = sorted(loading_info["missing_keys"])
        if missing:
            raise RuntimeError(
                f"{model_id} is not a checkpoint for transformers' Florence-2 implementation: "
                f"{len(missing)} weights are missing, e.g. {missing[:3]}"
            )
        self.florence_model = model.to(self.device)
        self.florence_processor = Florence2Processor.from_pretrained(model_id, revision=revision)

    def shutdown(self) -> None:
        self.gdino_model = None
        self.sam2_predictor = None
        self.florence_model = None
        self.florence_processor = None
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
