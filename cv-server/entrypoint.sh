#!/bin/bash
set -e

# Fail before downloading anything: the API refuses to start without a token.
if [ -z "${CV_SERVER_API_TOKEN:-}" ]; then
    echo "CV_SERVER_API_TOKEN is not set. Every endpoint, /v1/health included, requires a bearer token." >&2
    exit 1
fi

# Download the GroundingDINO / SAM 2 files on first start, or re-verify them against
# their pinned SHA-256 (a no-op download when they are already in place). Set
# CV_SERVER_FETCH_WEIGHTS=0 to skip, e.g. on an offline host given the files another way.
if [ "${CV_SERVER_FETCH_WEIGHTS:-1}" != "0" ]; then
    python3 /app/scripts/fetch_weights.py --root /app
fi

# TLS is on when SSL_CERTFILE and SSL_KEYFILE both name existing files. Then the
# server speaks HTTPS only, on :443, and starts no plain-HTTP listener, so the bearer
# token and the images never cross the network in clear text. TLS is off when neither
# file exists; only then does it serve plain HTTP, on :8000. One file without the
# other is a configuration error. CV_SERVER_REQUIRE_TLS=1 refuses to start without TLS.
tls_files=0
if [ -n "${SSL_CERTFILE:-}" ] && [ -f "$SSL_CERTFILE" ]; then tls_files=$((tls_files + 1)); fi
if [ -n "${SSL_KEYFILE:-}" ] && [ -f "$SSL_KEYFILE" ]; then tls_files=$((tls_files + 1)); fi

if [ "$tls_files" -eq 2 ]; then
    echo "TLS on: serving HTTPS only on :443 (no plain-HTTP listener)"
    exec uvicorn app.main:app --host 0.0.0.0 --port 443 \
        --ssl-certfile "$SSL_CERTFILE" \
        --ssl-keyfile "$SSL_KEYFILE"
elif [ "$tls_files" -eq 1 ]; then
    echo "TLS misconfigured: only one of SSL_CERTFILE and SSL_KEYFILE exists. Provide both, or neither for plain HTTP." >&2
    exit 1
elif [ "${CV_SERVER_REQUIRE_TLS:-0}" = "1" ]; then
    echo "CV_SERVER_REQUIRE_TLS=1 but no certificate and key were found at SSL_CERTFILE / SSL_KEYFILE." >&2
    exit 1
else
    echo "TLS off (no certificate and key found): serving plain HTTP on :8000"
    exec uvicorn app.main:app --host 0.0.0.0 --port 8000
fi
