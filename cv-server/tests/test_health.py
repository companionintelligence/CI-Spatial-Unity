from fastapi.testclient import TestClient

from app.main import app


def test_health_endpoint() -> None:
    client = TestClient(app)
    response = client.get("/v1/health")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ok"
    assert "gpu" in body
    assert "vram_used_mb" in body
    assert "vram_total_mb" in body
    assert "models_loaded" in body
