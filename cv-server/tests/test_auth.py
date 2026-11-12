import json

import pytest
from fastapi.testclient import TestClient
from pydantic import SecretStr
from starlette.websockets import WebSocketDisconnect

from app.auth import require_configured_token
from app.config import settings
from app.main import app, startup

client = TestClient(app)


def test_health_requires_token(auth_headers: dict) -> None:
    # The health payload names the GPU and the loaded models, so it is protected too.
    response = client.get("/v1/health")
    assert response.status_code == 401
    assert response.headers["www-authenticate"].startswith("Bearer")
    response = client.get("/v1/health", headers=auth_headers)
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


@pytest.mark.parametrize(
    "method,path",
    [
        ("post", "/v1/detect"),
        ("post", "/v1/detect/batch"),
        ("post", "/v1/segment"),
        ("post", "/v1/florence"),
        ("post", "/v1/gltf"),
        ("get", "/openapi.json"),
        ("get", "/docs"),
        ("get", "/v1/does-not-exist"),
        ("get", "/v1/health"),
    ],
)
def test_protected_routes_reject_missing_token(method: str, path: str) -> None:
    response = getattr(client, method)(path)
    assert response.status_code == 401
    assert response.headers["www-authenticate"].startswith("Bearer")


@pytest.mark.parametrize(
    "authorization",
    ["Bearer wrong-token", "Basic dXNlcjpwYXNz", "Bearer", "", "Token {token}"],
)
def test_protected_routes_reject_bad_credentials(authorization: str, api_token: str) -> None:
    headers = {"Authorization": authorization.format(token=api_token)}
    response = client.post("/v1/detect", json={}, headers=headers)
    assert response.status_code == 401


def test_valid_token_reaches_route(auth_headers: dict) -> None:
    # An empty body fails validation, which proves the request got past auth
    # without needing model weights.
    response = client.post("/v1/detect", json={}, headers=auth_headers)
    assert response.status_code == 422


def test_scheme_is_case_insensitive(api_token: str) -> None:
    response = client.post("/v1/detect", json={}, headers={"Authorization": f"bearer {api_token}"})
    assert response.status_code == 422


def test_any_configured_token_is_accepted(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(settings, "api_token", SecretStr("old-token, new-token"))
    for token in ("old-token", "new-token"):
        response = client.post("/v1/detect", json={}, headers={"Authorization": f"Bearer {token}"})
        assert response.status_code == 422
    response = client.post("/v1/detect", json={}, headers={"Authorization": "Bearer old-token, new-token"})
    assert response.status_code == 401


def test_gltf_upload_with_token(auth_headers: dict, monkeypatch: pytest.MonkeyPatch, tmp_path) -> None:
    monkeypatch.setattr(settings, "gltf_upload_dir", str(tmp_path))
    response = client.post(
        "/v1/gltf",
        files={"gltf": ("scan.gltf", b'{"asset": {"version": "2.0"}}', "model/gltf+json")},
        headers=auth_headers,
    )
    assert response.status_code == 200
    assert response.json()["ok"] is True


def test_unconfigured_server_fails_closed(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(settings, "api_token", SecretStr(""))
    assert client.get("/v1/health").status_code == 503
    response = client.post("/v1/detect", json={}, headers={"Authorization": "Bearer "})
    assert response.status_code == 503
    with pytest.raises(RuntimeError, match="CV_SERVER_API_TOKEN"):
        require_configured_token()
    with pytest.raises(RuntimeError, match="CV_SERVER_API_TOKEN"):
        startup()


def test_websocket_rejects_missing_token() -> None:
    with pytest.raises(WebSocketDisconnect) as excinfo:
        with client.websocket_connect("/v1/stream") as websocket:
            websocket.receive_text()
    assert excinfo.value.code == 1008


def test_websocket_rejects_wrong_token() -> None:
    with pytest.raises(WebSocketDisconnect):
        with client.websocket_connect(
            "/v1/stream", headers={"Authorization": "Bearer wrong-token"}
        ) as websocket:
            websocket.receive_text()


def test_websocket_accepts_authorization_header(auth_headers: dict) -> None:
    with client.websocket_connect("/v1/stream", headers=auth_headers) as websocket:
        websocket.send_text(json.dumps({"mode": "unknown"}))
        assert websocket.receive_json() == {"error": "Unsupported mode: unknown"}


def test_websocket_accepts_bearer_subprotocol(api_token: str) -> None:
    with client.websocket_connect("/v1/stream", subprotocols=["bearer", api_token]) as websocket:
        assert websocket.accepted_subprotocol == "bearer"
        websocket.send_text(json.dumps({"mode": "unknown"}))
        assert websocket.receive_json() == {"error": "Unsupported mode: unknown"}
