"""Request size limits: every HTTP body is capped at settings.max_request_bytes."""

import json

import pytest
from fastapi.testclient import TestClient

from app.config import settings
from app.main import app

client = TestClient(app)

LIMIT = 4096


@pytest.fixture(autouse=True)
def small_limit(monkeypatch: pytest.MonkeyPatch) -> int:
    monkeypatch.setattr(settings, "max_request_bytes", LIMIT)
    return LIMIT


def test_default_limit_is_32_mib() -> None:
    from app.config import Settings

    assert Settings().max_request_bytes == 32 * 1024 * 1024


def test_body_under_the_limit_reaches_the_route(auth_headers: dict) -> None:
    # An empty JSON object fails validation (422), which proves the body got through.
    response = client.post("/v1/detect", json={}, headers=auth_headers)
    assert response.status_code == 422


def test_declared_oversize_body_is_refused_before_reading(auth_headers: dict) -> None:
    body = json.dumps({"image": "A" * (LIMIT * 2), "prompt": "chair ."})
    response = client.post(
        "/v1/detect", content=body, headers={**auth_headers, "Content-Type": "application/json"}
    )
    assert response.status_code == 413
    assert str(LIMIT) in response.json()["detail"]


def test_chunked_oversize_body_is_refused_while_streaming(auth_headers: dict) -> None:
    def chunks():
        for _ in range(8):
            yield b"A" * 1024

    # A generator body is sent without Content-Length (chunked), so only the byte count stops it.
    response = client.post(
        "/v1/detect", content=chunks(), headers={**auth_headers, "Content-Type": "application/json"}
    )
    assert response.status_code == 413


def test_oversize_gltf_upload_is_refused_and_not_written(
    auth_headers: dict, monkeypatch: pytest.MonkeyPatch, tmp_path
) -> None:
    monkeypatch.setattr(settings, "gltf_upload_dir", str(tmp_path))
    response = client.post(
        "/v1/gltf",
        files={
            "gltf": ("scan.gltf", b'{"asset": {"version": "2.0"}}', "model/gltf+json"),
            "bin": ("scan.bin", b"\0" * (LIMIT * 2), "application/octet-stream"),
        },
        headers=auth_headers,
    )
    assert response.status_code == 413
    assert list(tmp_path.iterdir()) == []


def test_gltf_upload_under_the_limit_is_stored(
    auth_headers: dict, monkeypatch: pytest.MonkeyPatch, tmp_path
) -> None:
    monkeypatch.setattr(settings, "gltf_upload_dir", str(tmp_path))
    response = client.post(
        "/v1/gltf",
        files={"gltf": ("scan.gltf", b'{"asset": {"version": "2.0"}}', "model/gltf+json")},
        headers=auth_headers,
    )
    assert response.status_code == 200
    assert len(list(tmp_path.iterdir())) == 1


def test_invalid_content_length_is_rejected(auth_headers: dict) -> None:
    response = client.post(
        "/v1/detect",
        content=b"{}",
        headers={**auth_headers, "Content-Type": "application/json", "Content-Length": "-1"},
    )
    assert response.status_code == 400


def test_token_is_checked_before_the_size(auth_headers: dict) -> None:
    body = json.dumps({"image": "A" * (LIMIT * 2)})
    response = client.post("/v1/detect", content=body, headers={"Content-Type": "application/json"})
    assert response.status_code == 401
