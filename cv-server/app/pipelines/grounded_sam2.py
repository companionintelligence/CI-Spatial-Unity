from typing import List, Tuple

import numpy as np
from PIL import Image

from app.model_manager import ModelManager
from app.schemas.segment import SegmentAnnotation, SegmentRequest, SegmentResponse
from app.utils.boxes import cxcywh_to_xyxy, xyxy_to_pixels
from app.utils.image_utils import decode_base64_image
from app.utils.mask_encoding import rle_encode

try:
    from groundingdino.util.inference import predict
    from groundingdino.datasets import transforms as T
except Exception:  # pragma: no cover
    predict = None
    T = None


def _prepare_grounding_dino_input(image: Image.Image) -> Tuple[np.ndarray, "torch.Tensor"]:
    if T is None:
        raise RuntimeError("GroundingDINO transforms are not available.")
    transform = T.Compose(
        [
            T.RandomResize([800], max_size=1333),
            T.ToTensor(),
            T.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
        ]
    )
    image_np = np.asarray(image)
    image_tensor, _ = transform(image, None)
    return image_np, image_tensor


def run_grounded_sam2(request: SegmentRequest) -> SegmentResponse:
    if predict is None:
        raise RuntimeError("GroundingDINO inference is not available.")

    image = decode_base64_image(request.image, max_side=request.max_side)
    image_np, image_tensor = _prepare_grounding_dino_input(image)
    height, width = image_np.shape[:2]

    mm = ModelManager.get_instance()
    mm.load_grounding_dino()
    boxes, scores, labels = predict(
        model=mm.gdino_model,
        image=image_tensor,
        caption=request.prompt,
        box_threshold=request.box_threshold,
        text_threshold=request.text_threshold,
        device=str(mm.device),
    )

    annotations: List[SegmentAnnotation] = []
    # predict() gives (cx, cy, w, h) as fractions; the API returns [x1, y1, x2, y2]
    # fractions (see app/utils/boxes.py), and SAM 2 takes the corners in pixels.
    bboxes = cxcywh_to_xyxy(boxes)
    pixel_boxes = xyxy_to_pixels(bboxes, width, height)

    if request.return_masks:
        mm.load_sam2()
        mm.sam2_predictor.set_image(image_np)

    for idx, box in enumerate(bboxes):
        mask_obj = None
        if request.return_masks:
            mask, score, _ = mm.sam2_predictor.predict(
                box=pixel_boxes[idx],
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
