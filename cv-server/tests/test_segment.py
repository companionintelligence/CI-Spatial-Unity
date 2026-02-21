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
def test_segment_endpoint_smoke() -> None:
    client = TestClient(app)
    payload = {
        "image": _build_test_image_b64(),
        "prompt": "chair . table .",
        "box_threshold": 0.35,
        "text_threshold": 0.25,
        "grounding_model": "grounding_dino",
        "return_masks": False,
        "mask_format": "rle",
    }
    response = client.post("/v1/segment", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert "annotations" in body
    assert "image_size" in body
