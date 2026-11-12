"""Bearer-token authentication for the CV server.

Every HTTP and WebSocket route requires ``Authorization: Bearer <token>``,
including the health check, which reports the GPU model, VRAM use, loaded models
and uptime. Accepted tokens come from the ``CV_SERVER_API_TOKEN`` environment
variable, comma-separated so a token can be rotated without downtime. With no
token configured the server fails closed: the startup hook refuses to start and
every route answers 503.

Browsers cannot set headers on a WebSocket handshake, so ``/v1/stream`` also
accepts the token as a subprotocol pair: ``new WebSocket(url, ["bearer", token])``.
The server then selects the ``bearer`` subprotocol. Tokens are never accepted in
the query string, because access logs record it.
"""

from __future__ import annotations

import hmac
from typing import Iterable, List, Optional, Sequence

from starlette import status
from starlette.datastructures import Headers
from starlette.responses import JSONResponse
from starlette.types import ASGIApp, Message, Receive, Scope, Send
from starlette.websockets import WebSocketClose

from app.config import settings

#: Paths reachable without a token: none. Probes send the token like any client
#: (curl -H "Authorization: Bearer $CV_SERVER_API_TOKEN" .../v1/health).
PUBLIC_PATHS: frozenset = frozenset()

#: WebSocket subprotocol that carries the token in the next offered subprotocol.
WS_BEARER_SUBPROTOCOL = "bearer"

TOKEN_ENV_VAR = "CV_SERVER_API_TOKEN"


def configured_tokens() -> List[bytes]:
    """Return the accepted tokens; empty when CV_SERVER_API_TOKEN is unset."""
    raw = settings.api_token.get_secret_value()
    return [token.strip().encode("utf-8") for token in raw.split(",") if token.strip()]


def require_configured_token() -> None:
    """Refuse to run without a token, so the API is never served unauthenticated."""
    if not configured_tokens():
        raise RuntimeError(
            f"{TOKEN_ENV_VAR} is not set. Set it to a bearer token (or several, "
            "comma-separated); every endpoint requires one."
        )


def is_valid_token(candidate: Optional[str], tokens: Sequence[bytes]) -> bool:
    if not candidate or not tokens:
        return False
    supplied = candidate.encode("utf-8")
    matched = False
    for token in tokens:
        # Compare against every token so timing does not reveal which one matched.
        matched |= hmac.compare_digest(supplied, token)
    return matched


def _token_from_authorization(headers: Headers) -> Optional[str]:
    scheme, _, credentials = headers.get("authorization", "").partition(" ")
    if scheme.lower() != "bearer":
        return None
    return credentials.strip() or None


def _token_from_subprotocols(offered: Sequence[str]) -> Optional[str]:
    if WS_BEARER_SUBPROTOCOL not in offered:
        return None
    index = offered.index(WS_BEARER_SUBPROTOCOL) + 1
    return offered[index] if index < len(offered) else None


def _select_bearer_subprotocol(send: Send) -> Send:
    """Echo the ``bearer`` subprotocol on accept; browsers drop the socket otherwise."""

    async def wrapped(message: Message) -> None:
        if message["type"] == "websocket.accept" and not message.get("subprotocol"):
            message = {**message, "subprotocol": WS_BEARER_SUBPROTOCOL}
        await send(message)

    return wrapped


class BearerTokenMiddleware:
    """ASGI middleware that rejects requests without a valid bearer token.

    It wraps the whole application, so routes added later (and FastAPI's
    ``/docs`` and ``/openapi.json``) are protected by default.
    """

    def __init__(self, app: ASGIApp, public_paths: Iterable[str] = PUBLIC_PATHS) -> None:
        self.app = app
        self.public_paths = frozenset(public_paths)

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] not in ("http", "websocket") or scope["path"] in self.public_paths:
            await self.app(scope, receive, send)
            return

        tokens = configured_tokens()
        headers = Headers(scope=scope)

        if scope["type"] == "websocket":
            offered = [str(p).strip() for p in scope.get("subprotocols") or []]
            candidates = (_token_from_authorization(headers), _token_from_subprotocols(offered))
            if not any(is_valid_token(candidate, tokens) for candidate in candidates):
                await WebSocketClose(code=status.WS_1008_POLICY_VIOLATION)(scope, receive, send)
                return
            if WS_BEARER_SUBPROTOCOL in offered:
                send = _select_bearer_subprotocol(send)
            await self.app(scope, receive, send)
            return

        if not tokens:
            response = JSONResponse(
                {"detail": f"Authentication is not configured: set {TOKEN_ENV_VAR}."},
                status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            )
        elif not is_valid_token(_token_from_authorization(headers), tokens):
            response = JSONResponse(
                {"detail": "Missing or invalid bearer token."},
                status_code=status.HTTP_401_UNAUTHORIZED,
                headers={"WWW-Authenticate": 'Bearer realm="cv-server"'},
            )
        else:
            await self.app(scope, receive, send)
            return
        await response(scope, receive, send)
