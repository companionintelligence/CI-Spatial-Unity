from typing import Any, Dict

import numpy as np
import torch

from app.model_manager import ModelManager
from app.schemas.florence import FlorenceRequest, FlorenceResponse
from app.utils.image_utils import decode_base64_image


def run_florence2(request: FlorenceRequest) -> FlorenceResponse:
    mm = ModelManager.get_instance()
    mm.load_florence2()
    if mm.florence_model is None or mm.florence_processor is None:
        raise RuntimeError("Florence-2 model is not loaded.")

    image = decode_base64_image(request.image, max_side=request.max_side)
    inputs = mm.florence_processor(text=request.task + request.text_input, images=image, return_tensors="pt")
    inputs = {k: v.to(mm.device) for k, v in inputs.items()}

    with torch.no_grad():
        generated_ids = mm.florence_model.generate(**inputs, max_new_tokens=512)
    generated_text = mm.florence_processor.batch_decode(generated_ids, skip_special_tokens=True)[0]

    results: Dict[str, Any] = {"raw_text": generated_text}
    return FlorenceResponse(task=request.task, results=results, text=generated_text)
