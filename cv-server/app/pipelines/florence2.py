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
    inputs = {
        k: (v.to(mm.device, dtype=torch.float16) if k == "pixel_values" else v.to(mm.device))
        for k, v in inputs.items()
    }

    with torch.no_grad():
        generated_ids = mm.florence_model.generate(**inputs, max_new_tokens=512)
    generated_text = mm.florence_processor.batch_decode(generated_ids, skip_special_tokens=False)[0]

    # Post-process the generated text through Florence's built-in decoder
    # which converts location tokens (<loc_NNN>) into actual bounding boxes,
    # polygon points, etc.
    parsed = mm.florence_processor.post_process_generation(
        generated_text,
        task=request.task,
        image_size=(image.width, image.height),
    )

    # parsed is keyed by the task prompt, e.g. {"<OD>": {"labels": [...], "bboxes": [...]}}
    task_result = parsed.get(request.task, {})

    # Normalise bboxes from pixel coords to 0-1 so the Unity client is format-agnostic
    if "bboxes" in task_result:
        w, h = image.width, image.height
        normalised = []
        for box in task_result["bboxes"]:
            if len(box) >= 4:
                normalised.append([box[0] / w, box[1] / h, box[2] / w, box[3] / h])
        task_result["bboxes"] = normalised

    # Also include the raw decoded text for caption-only tasks
    plain_text = mm.florence_processor.batch_decode(generated_ids, skip_special_tokens=True)[0]
    results: Dict[str, Any] = {**task_result, "raw_text": plain_text}
    return FlorenceResponse(task=request.task, results=results, text=plain_text)
