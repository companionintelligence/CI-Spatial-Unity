#!/bin/bash
set -e

# Fail before downloading anything: the API refuses to start without a token.
if [ -z "${CV_SERVER_API_TOKEN:-}" ]; then
    echo "CV_SERVER_API_TOKEN is not set. Every endpoint except /v1/health requires a bearer token." >&2
    exit 1
fi

# Download the GroundingDINO / SAM 2 files on first start, or re-verify them against
# their pinned SHA-256 (a no-op download when they are already in place). Set
# CV_SERVER_FETCH_WEIGHTS=0 to skip, e.g. on an offline host given the files another way.
if [ "${CV_SERVER_FETCH_WEIGHTS:-1}" != "0" ]; then
    python3 /app/scripts/fetch_weights.py --root /app
fi

# Always start HTTP on port 8000
if [ -n "$SSL_CERTFILE" ] && [ -n "$SSL_KEYFILE" ] && [ -f "$SSL_CERTFILE" ] && [ -f "$SSL_KEYFILE" ]; then
    echo "Starting HTTPS on :443 and HTTP on :8000"
    uvicorn app.main:app --host 0.0.0.0 --port 443 \
        --ssl-certfile "$SSL_CERTFILE" \
        --ssl-keyfile "$SSL_KEYFILE" &
    uvicorn app.main:app --host 0.0.0.0 --port 8000
else
    echo "No TLS certs found, starting HTTP only on :8000"
    uvicorn app.main:app --host 0.0.0.0 --port 8000
fi
