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
def test_stream_detect() -> None:
    client = TestClient(app)
    with client.websocket_connect("/v1/stream") as websocket:
        websocket.send_json(
            {
                "mode": "detect",
                "data": {
                    "image": _build_test_image_b64(),
                    "prompt": "chair . table .",
                    "box_threshold": 0.35,
                    "text_threshold": 0.25,
                    "max_side": 64,
                },
            }
        )
        response = websocket.receive_json()
        assert "bboxes" in response
        assert "labels" in response
        assert "scores" in response
