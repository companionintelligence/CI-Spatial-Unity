"""Request size limits for the CV server.

Every HTTP request body is capped at ``settings.max_request_bytes``
(``CV_SERVER_MAX_REQUEST_BYTES``, default 32 MiB). That covers the base64 images
sent to /v1/detect, /v1/detect/batch, /v1/segment and /v1/florence, and the
multipart glTF upload on /v1/gltf, which is written to disk.

A request whose Content-Length is over the cap is refused with 413 before any of
the body is read. A body without a Content-Length (chunked) is counted as it
arrives and refused with 413 as soon as it passes the cap, so no request can fill
the server's memory or disk. WebSocket messages on /v1/stream are capped by
uvicorn's ``--ws-max-size`` (``CV_SERVER_MAX_WS_MESSAGE_BYTES``, see entrypoint.sh).
"""

from __future__ import annotations

from typing import Optional

from fastapi import HTTPException
from starlette import status
from starlette.datastructures import Headers
from starlette.responses import JSONResponse
from starlette.types import ASGIApp, Message, Receive, Scope, Send

from app.config import settings


def too_large_detail(limit: int) -> str:
    return f"Request body is larger than the server's {limit}-byte limit."


class RequestSizeLimitMiddleware:
    """ASGI middleware that refuses HTTP request bodies over a byte limit."""

    def __init__(self, app: ASGIApp, max_bytes: Optional[int] = None) -> None:
        self.app = app
        self._max_bytes = max_bytes

    @property
    def max_bytes(self) -> int:
        return self._max_bytes if self._max_bytes is not None else settings.max_request_bytes

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        limit = self.max_bytes
        declared = Headers(scope=scope).get("content-length")
        if declared is not None:
            try:
                declared_bytes = int(declared)
            except ValueError:
                declared_bytes = -1
            if declared_bytes < 0:
                response = JSONResponse(
                    {"detail": "Invalid Content-Length header."},
                    status_code=status.HTTP_400_BAD_REQUEST,
                )
                await response(scope, receive, send)
                return
            if declared_bytes > limit:
                response = JSONResponse(
                    {"detail": too_large_detail(limit)},
                    status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                )
                await response(scope, receive, send)
                return

        received = 0

        async def limited_receive() -> Message:
            nonlocal received
            message = await receive()
            if message["type"] == "http.request":
                received += len(message.get("body", b""))
                if received > limit:
                    # FastAPI re-raises HTTPException from body parsing (JSON and
                    # multipart alike), so the client gets this 413, not a generic 400.
                    raise HTTPException(
                        status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                        detail=too_large_detail(limit),
                    )
            return message

        await self.app(scope, limited_receive, send)
