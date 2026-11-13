"""The boxes the API returns: [x1, y1, x2, y2] corners as fractions of the image size.

GroundingDINO's predict() returns boxes as (cx, cy, w, h). These tests swap predict()
and the models for stand-ins that return known boxes, then check the boxes that
/v1/detect, /v1/detect/batch, /v1/segment and WS /v1/stream send back, and the box
prompts SAM 2 receives. They need no model files and no GPU.
"""

import base64
from io import BytesIO
from types import SimpleNamespace
from typing import List, Optional

import numpy as np
import pytest
import torch
from fastapi.testclient import TestClient
from PIL import Image

from app.main import app
from app.model_manager import ModelManager
from app.pipelines import grounded_sam2, grounding_dino

# What GroundingDINO's predict() returns for three objects: (cx, cy, w, h), as fractions
# of the image width (cx, w) and height (cy, h).
GDINO_CXCYWH = [
    [0.25, 0.50, 0.20, 0.40],
    [0.30, 0.20, 0.50, 0.30],  # wider than the distance from its centre to the left edge
    [0.90, 0.90, 0.40, 0.40],  # runs past the right and bottom edges
]
LABELS = ["shelf", "basket", "box"]
SCORES = [0.81, 0.62, 0.47]

# The same boxes as the API returns them: [x1, y1, x2, y2], clipped to the image.
EXPECTED_XYXY = [
    [0.15, 0.30, 0.35, 0.70],
    [0.05, 0.05, 0.55, 0.35],
    [0.70, 0.70, 1.00, 1.00],
]

# The width differs from the height, so swapped axes show up.
WIDTH, HEIGHT = 200, 100

# (sent width, sent height, max_side): the second case is shrunk by the server to
# WIDTH x HEIGHT first. Fractions are the same either way.
IMAGE_CASES = [(WIDTH, HEIGHT, None), (2 * WIDTH, 2 * HEIGHT, WIDTH)]
IMAGE_IDS = ["as-sent", "shrunk-by-max_side"]


def _jpeg_b64(width: int, height: int) -> str:
    image = Image.new("RGB", (width, height), (255, 255, 255))
    buffer = BytesIO()
    image.save(buffer, format="JPEG")
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def _payload(width: int, height: int, max_side: Optional[int], **extra) -> dict:
    payload = {"image": _jpeg_b64(width, height), "prompt": "shelf . basket . box ."}
    if max_side is not None:
        payload["max_side"] = max_side
    payload.update(extra)
    return payload


def _assert_boxes(boxes: List[List[float]]) -> None:
    assert len(boxes) == len(EXPECTED_XYXY)
    for box, expected in zip(boxes, EXPECTED_XYXY):
        assert box == pytest.approx(expected, abs=1e-6)
        x1, y1, x2, y2 = box
        assert 0.0 <= x1 <= x2 <= 1.0
        assert 0.0 <= y1 <= y2 <= 1.0


def _foreground_pixels(counts: str) -> int:
    """Pixels set in an RLE mask from app.utils.mask_encoding (runs start with zeros)."""
    runs = [int(c) for c in counts.split()]
    return sum(runs[1::2])


class FakeSam2Predictor:
    """Records the box prompts and returns the prompted rectangle as the mask."""

    def __init__(self) -> None:
        self.image_shape = None
        self.boxes: List[List[float]] = []

    def set_image(self, image) -> None:
        self.image_shape = np.asarray(image).shape[:2]

    def predict(self, box=None, multimask_output=True, **kwargs):
        box = np.asarray(box, dtype=np.float64)
        self.boxes.append(box.tolist())
        height, width = self.image_shape
        mask = np.zeros((1, height, width), dtype=bool)
        x1, y1, x2, y2 = (max(int(v), 0) for v in np.round(box))
        mask[0, y1:y2, x1:x2] = True
        return mask, np.array([0.9]), None


@pytest.fixture
def fake_models(monkeypatch: pytest.MonkeyPatch) -> SimpleNamespace:
    """Stand in for GroundingDINO and SAM 2 so the pipelines run without model files."""

    def fake_predict(**kwargs):
        # Same types as groundingdino.util.inference.predict: boxes and scores as tensors.
        return torch.tensor(GDINO_CXCYWH), torch.tensor(SCORES), list(LABELS)

    def fake_prepare(image):
        return np.asarray(image), None

    for module in (grounding_dino, grounded_sam2):
        monkeypatch.setattr(module, "predict", fake_predict)
        monkeypatch.setattr(module, "_prepare_grounding_dino_input", fake_prepare)

    sam2 = FakeSam2Predictor()
    manager = SimpleNamespace(
        device="cpu",
        gdino_model=object(),
        sam2_predictor=sam2,
        load_grounding_dino=lambda: None,
        load_sam2=lambda: None,
    )
    monkeypatch.setattr(ModelManager, "_instance", manager)
    return SimpleNamespace(sam2=sam2)


@pytest.mark.parametrize("width,height,max_side", IMAGE_CASES, ids=IMAGE_IDS)
def test_detect_returns_corners_as_fractions(
    auth_headers: dict, fake_models: SimpleNamespace, width: int, height: int, max_side: Optional[int]
) -> None:
    client = TestClient(app)
    response = client.post("/v1/detect", json=_payload(width, height, max_side), headers=auth_headers)
    assert response.status_code == 200, response.text
    body = response.json()
    _assert_boxes(body["bboxes"])
    assert body["labels"] == LABELS
    assert body["scores"] == pytest.approx(SCORES)


def test_detect_batch_returns_corners_as_fractions(auth_headers: dict, fake_models: SimpleNamespace) -> None:
    client = TestClient(app)
    payload = [_payload(width, height, max_side) for width, height, max_side in IMAGE_CASES]
    response = client.post("/v1/detect/batch", json=payload, headers=auth_headers)
    assert response.status_code == 200, response.text
    body = response.json()
    assert len(body) == len(IMAGE_CASES)
    for result in body:
        _assert_boxes(result["bboxes"])


def test_stream_detect_returns_corners_as_fractions(auth_headers: dict, fake_models: SimpleNamespace) -> None:
    client = TestClient(app)
    with client.websocket_connect("/v1/stream", headers=auth_headers) as websocket:
        websocket.send_json({"mode": "detect", "data": _payload(WIDTH, HEIGHT, None)})
        body = websocket.receive_json()
    _assert_boxes(body["bboxes"])


@pytest.mark.parametrize("width,height,max_side", IMAGE_CASES, ids=IMAGE_IDS)
def test_segment_returns_fractions_and_prompts_sam2_with_pixel_corners(
    auth_headers: dict, fake_models: SimpleNamespace, width: int, height: int, max_side: Optional[int]
) -> None:
    client = TestClient(app)
    payload = _payload(width, height, max_side, return_masks=True)
    response = client.post("/v1/segment", json=payload, headers=auth_headers)
    assert response.status_code == 200, response.text
    body = response.json()

    # image_size is [height, width] of the image the masks were computed on.
    assert body["image_size"] == [HEIGHT, WIDTH]
    annotations = body["annotations"]
    _assert_boxes([a["bbox"] for a in annotations])
    assert [a["label"] for a in annotations] == LABELS

    # SAM 2 is prompted with the same corners, in pixels of the image it segments.
    expected_pixels = [[x1 * WIDTH, y1 * HEIGHT, x2 * WIDTH, y2 * HEIGHT] for x1, y1, x2, y2 in EXPECTED_XYXY]
    assert len(fake_models.sam2.boxes) == len(expected_pixels)
    for prompt, expected in zip(fake_models.sam2.boxes, expected_pixels):
        assert prompt == pytest.approx(expected, abs=1e-3)

    # Each mask covers its box: the prompted rectangle, in the image_size grid.
    for annotation, (x1, y1, x2, y2) in zip(annotations, expected_pixels):
        assert annotation["mask"]["size"] == [HEIGHT, WIDTH]
        expected_area = round(x2 - x1) * round(y2 - y1)
        assert _foreground_pixels(annotation["mask"]["counts"]) == expected_area
