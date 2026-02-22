#!/bin/bash
set -e

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
