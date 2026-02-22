import base64
from io import BytesIO
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from PIL import Image

from app.main import app
from app.config import settings


def _build_test_image_b64() -> str:
    image = Image.new("RGB", (64, 64), (255, 255, 255))
    buffer = BytesIO()
    image.save(buffer, format="JPEG")
    return base64.b64encode(buffer.getvalue()).decode("utf-8")


@pytest.mark.skipif(
    not Path(settings.gdino_weights_path).exists(),
    reason="GroundingDINO weights not found",
)
def test_detect_endpoint_smoke() -> None:
    client = TestClient(app)
    payload = {
        "image": _build_test_image_b64(),
        "prompt": "chair . table .",
        "box_threshold": 0.35,
        "text_threshold": 0.25,
    }
    response = client.post("/v1/detect", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert "bboxes" in body
    assert "labels" in body
    assert "scores" in body


@pytest.mark.skipif(
    not Path(settings.gdino_weights_path).exists(),
    reason="GroundingDINO weights not found",
)
def test_detect_batch_endpoint_smoke() -> None:
    client = TestClient(app)
    payload = [
        {
            "image": _build_test_image_b64(),
            "prompt": "chair . table .",
            "box_threshold": 0.35,
            "text_threshold": 0.25,
            "max_side": 64,
        },
        {
            "image": _build_test_image_b64(),
            "prompt": "lamp .",
            "box_threshold": 0.35,
            "text_threshold": 0.25,
        },
    ]
    response = client.post("/v1/detect/batch", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert isinstance(body, list)
    assert len(body) == 2
