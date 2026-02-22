from __future__ import annotations

from typing import Optional

import torch

from app.config import settings

_gdino_import_error = None
try:
    from groundingdino.util.inference import load_model
except Exception as exc:  # pragma: no cover - optional import until deps are installed
    load_model = None
    _gdino_import_error = exc

try:
    from sam2.build_sam2 import build_sam2
    from sam2.sam2_image_predictor import SAM2ImagePredictor
except Exception:  # pragma: no cover
    build_sam2 = None
    SAM2ImagePredictor = None

try:
    from transformers import AutoModelForCausalLM, AutoProcessor
except Exception:  # pragma: no cover
    AutoModelForCausalLM = None
    AutoProcessor = None


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
        if load_model is None:
            detail = f"{type(_gdino_import_error).__name__}: {_gdino_import_error}" if _gdino_import_error else "Unknown import error"
            raise RuntimeError(f"GroundingDINO dependencies are not available. {detail}")
        self.gdino_model = load_model(
            settings.gdino_config_path,
            settings.gdino_weights_path,
            device=str(self.device),
        )

    def load_sam2(self) -> None:
        if self.sam2_predictor is not None:
            return
        if build_sam2 is None or SAM2ImagePredictor is None:
            raise RuntimeError("SAM2 dependencies are not available.")
        sam2_model = build_sam2(
            settings.sam2_config_path,
            settings.sam2_weights_path,
            device=str(self.device),
        )
        self.sam2_predictor = SAM2ImagePredictor(sam2_model)

    def load_florence2(self) -> None:
        if self.florence_model is not None:
            return
        if AutoModelForCausalLM is None or AutoProcessor is None:
            raise RuntimeError("Transformers dependencies are not available.")
        self.florence_model = AutoModelForCausalLM.from_pretrained(
            settings.florence_model_id,
            torch_dtype=torch.float16,
            trust_remote_code=True,
        ).to(self.device)
        self.florence_processor = AutoProcessor.from_pretrained(
            settings.florence_model_id,
            trust_remote_code=True,
        )

    def shutdown(self) -> None:
        self.gdino_model = None
        self.sam2_predictor = None
        self.florence_model = None
        self.florence_processor = None
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
