from typing import List

import numpy as np

from app.model_manager import ModelManager
from app.schemas.detect import DetectRequest, DetectResponse
from app.utils.image_utils import decode_base64_image

try:
    from groundingdino.util.inference import predict
except Exception:  # pragma: no cover
    predict = None


def run_grounding_dino(request: DetectRequest) -> DetectResponse:
    if predict is None:
        raise RuntimeError("GroundingDINO inference is not available.")

    mm = ModelManager.get_instance()
    mm.load_grounding_dino()

    image = decode_base64_image(request.image, max_side=request.max_side)
    image_np = np.array(image)
    height, width = image_np.shape[:2]

    model = mm.gdino_model
    boxes, scores, labels = predict(
        model=model,
        image=image_np,
        caption=request.prompt,
        box_threshold=request.box_threshold,
        text_threshold=request.text_threshold,
        device=str(mm.device),
    )

    bboxes = _to_absolute_boxes(boxes, width, height)

    return DetectResponse(bboxes=bboxes, labels=labels, scores=[float(s) for s in scores])


def _to_absolute_boxes(boxes: np.ndarray, width: int, height: int) -> List[List[float]]:
    if boxes is None or len(boxes) == 0:
        return []
    abs_boxes: List[List[float]] = []
    for box in boxes:
        x1 = max(0.0, min(float(box[0]) * width, float(width)))
        y1 = max(0.0, min(float(box[1]) * height, float(height)))
        x2 = max(0.0, min(float(box[2]) * width, float(width)))
        y2 = max(0.0, min(float(box[3]) * height, float(height)))
        abs_boxes.append([x1, y1, x2, y2])
    return abs_boxes
