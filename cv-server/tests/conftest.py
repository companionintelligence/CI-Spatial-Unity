"""Shared test setup: every route except /v1/health needs the API token."""

import secrets

import pytest
from pydantic import SecretStr

from app.config import settings

# Generated per run so no token value lives in the repository.
_TEST_TOKEN = secrets.token_urlsafe(24)


@pytest.fixture(autouse=True)
def api_token(monkeypatch: pytest.MonkeyPatch) -> str:
    """Configure the server with a known token for each test."""
    monkeypatch.setattr(settings, "api_token", SecretStr(_TEST_TOKEN))
    return _TEST_TOKEN


@pytest.fixture
def auth_headers(api_token: str) -> dict:
    return {"Authorization": f"Bearer {api_token}"}
