import base64
from io import BytesIO

import pytest
from fastapi.testclient import TestClient
from PIL import Image

from app.main import app


def _build_test_image_b64() -> str:
    image = Image.new("RGB", (64, 64), (255, 255, 255))
    buffer = BytesIO()
    image.save(buffer, format="JPEG")
    return base64.b64encode(buffer.getvalue()).decode("utf-8")


@pytest.mark.skip(reason="Requires Florence-2 weights")
def test_florence_endpoint_smoke() -> None:
    client = TestClient(app)
    payload = {
        "image": _build_test_image_b64(),
        "task": "<CAPTION>",
        "text_input": "",
    }
    response = client.post("/v1/florence", json=payload)
    assert response.status_code == 200
