# CV Server

FastAPI server hosting Grounding DINO, SAM 2, and Florence-2 behind a single REST API.

## Quick Start

1) Set `CV_SERVER_API_TOKEN` (see [Authentication](#authentication)).
2) Build and run the container:

```bash
docker compose up --build
```

   On first start the container downloads the GroundingDINO and SAM 2 files
   into `cv-server/configs/` and `cv-server/checkpoints/` and verifies their
   SHA-256 (about 850 MB; later starts only re-verify). See
   [Model Files](#model-files).

3) Health check:

```bash
curl http://localhost:8000/v1/health
```

## Model Files

`scripts/fetch_weights.py` fetches the files the server loads from pinned URLs
(Hugging Face URLs pinned to a commit, with the upstream GitHub release or
download as fallback) and checks size and SHA-256 before moving each file into
place. It runs:

- on container start, from `entrypoint.sh` (set `CV_SERVER_FETCH_WEIGHTS=0` to skip);
- at build time with `docker build --build-arg FETCH_WEIGHTS_AT_BUILD=1 .`, which
  bakes the files into the image;
- by hand: `python scripts/fetch_weights.py` (writes under `cv-server/`; `--check`
  only verifies, `--force` replaces files that do not match).

Files go where the server reads them (`GDINO_CONFIG_PATH`, `GDINO_WEIGHTS_PATH`,
`SAM2_WEIGHTS_PATH`). A file already there with a different checksum is left
alone and reported unless `--force` is given. Florence-2 is downloaded by
`transformers` from the Hugging Face hub on first use and cached in the
`huggingface_cache` volume.

## Authentication

Every endpoint except `GET /v1/health` requires a bearer token:

```
Authorization: Bearer <token>
```

The server reads accepted tokens from `CV_SERVER_API_TOKEN` and refuses to start
without one. To rotate, set two comma-separated tokens, move clients to the new
one, then drop the old one. Generate a token with:

```bash
python -c "import secrets; print(secrets.token_urlsafe(32))"
```

Requests without a valid token get `401` (`503` if the server has no token
configured). `WS /v1/stream` takes the same `Authorization` header; browsers,
which cannot set headers on a WebSocket, offer the token as a subprotocol pair
instead: `new WebSocket(url, ["bearer", token])`. Tokens in query strings are
not accepted.

```bash
curl -H "Authorization: Bearer $CV_SERVER_API_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"image": "<BASE64_JPEG>", "prompt": "chair ."}' \
  http://localhost:8000/v1/detect
```

## Environment Variables

- `CV_SERVER_API_TOKEN` (required): accepted bearer token(s), comma-separated
- `CV_SERVER_FETCH_WEIGHTS` (default: `1`): fetch/verify model files on container start
- `CV_SERVER_DEVICE` (default: `cuda`)
- `GDINO_CONFIG_PATH`
- `GDINO_WEIGHTS_PATH`
- `SAM2_CONFIG_PATH`
- `SAM2_WEIGHTS_PATH`
- `FLORENCE_MODEL_ID`
- `BOX_THRESHOLD`
- `TEXT_THRESHOLD`
- `GLTF_UPLOAD_DIR` (default: `uploads/gltf`)

## Endpoints

- `POST /v1/detect`
- `POST /v1/detect/batch`
- `POST /v1/segment`
- `POST /v1/florence`
- `POST /v1/gltf`
- `GET /v1/health`
- `WS /v1/stream`

### glTF Upload

`POST /v1/gltf` accepts multipart form data:

- `gltf` (file)
- `bin` (file, optional)
- `extras` (field, optional)

The Quest app triggers this automatically when the user presses the **Y button**
on the left controller (Save & Export). See the main [README](../README.md) for
the full controller button mapping.

## Optional Request Fields

All image endpoints accept `max_side` to let the client request server-side resizing.

## Testing

```bash
pytest
```

The smoke tests skip automatically if model weights are not present.

## Load Testing

```bash
pip install -r load/requirements.txt
export CV_SERVER_API_TOKEN=<token>   # sent as the bearer token
locust -f load/locustfile.py --host http://localhost:8000 --image-b64 <BASE64_JPEG>
```
