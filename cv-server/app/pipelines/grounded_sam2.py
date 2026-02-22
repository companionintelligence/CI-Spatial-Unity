from typing import List

import numpy as np

from app.model_manager import ModelManager
from app.schemas.segment import SegmentAnnotation, SegmentRequest, SegmentResponse
from app.utils.image_utils import decode_base64_image
from app.utils.mask_encoding import rle_encode

try:
    from groundingdino.util.inference import predict
except Exception:  # pragma: no cover
    predict = None


def run_grounded_sam2(request: SegmentRequest) -> SegmentResponse:
    if predict is None:
        raise RuntimeError("GroundingDINO inference is not available.")

    image = decode_base64_image(request.image, max_side=request.max_side)
    image_np = np.array(image)
    height, width = image_np.shape[:2]

    mm = ModelManager.get_instance()
    boxes, scores, labels = predict(
        model=mm.gdino_model,
        image=image_np,
        caption=request.prompt,
        box_threshold=request.box_threshold,
        text_threshold=request.text_threshold,
        device=str(mm.device),
    )

    annotations: List[SegmentAnnotation] = []
    abs_boxes = _to_absolute_boxes(boxes, width, height)

    if request.return_masks:
        if mm.sam2_predictor is None:
            raise RuntimeError("SAM2 predictor is not loaded.")
        mm.sam2_predictor.set_image(image_np)

    for idx, box in enumerate(abs_boxes):
        mask_obj = None
        if request.return_masks:
            mask, score, _ = mm.sam2_predictor.predict(
                box=np.array(box),
                multimask_output=False,
            )
            mask_obj = rle_encode(mask[0])
        annotations.append(
            SegmentAnnotation(
                label=labels[idx],
                score=float(scores[idx]),
                bbox=box,
                mask=mask_obj,
            )
        )

    return SegmentResponse(annotations=annotations, image_size=[height, width])


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
