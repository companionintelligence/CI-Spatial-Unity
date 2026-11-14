# CV Server

FastAPI server hosting Grounding DINO, SAM 2, and Florence-2 behind a single REST API.

## Quick Start

1) Copy `.env.example` to `.env` and set `CV_SERVER_API_TOKEN` (see
   [Authentication](#authentication)). For HTTPS, also put a certificate and key
   in `cv-server/certs/` and point `CV_SERVER_SSL_CERTFILE` /
   `CV_SERVER_SSL_KEYFILE` at them (see [TLS](#tls)).
2) Build and run the container:

```bash
docker compose up --build
```

   On first start the container downloads the GroundingDINO and SAM 2 files
   into `cv-server/configs/` and `cv-server/checkpoints/` and verifies their
   SHA-256 (about 850 MB; later starts only re-verify). See
   [Model Files](#model-files).

3) Health check (it needs the token too):

```bash
curl -H "Authorization: Bearer $CV_SERVER_API_TOKEN" http://localhost:8000/v1/health
```

## Image

The image is `nvidia/cuda:12.1.1-cudnn8-devel-ubuntu22.04` with Python 3.11 from
the deadsnakes PPA, installed in a virtualenv at `/opt/venv` that comes first on
`PATH`. Ubuntu 22.04's default Python is 3.10, but `requirements.txt` pins
`pillow==13.0.0`, which needs Python 3.11 or later. Ubuntu 22.04's own `python3.11`
package is not used: it is 3.11.0rc1, a pre-release that gets no fixes, and
importing timm (used by GroundingDINO) crashes it.

Everything the image installs is pinned:

- PyTorch 2.5.1, torchvision 0.20.1 and torchaudio 2.5.1 from the cu121 index.
  2.5.1 is the last PyTorch release with CUDA 12.1 wheels.
- flash-attn 2.8.3.post2, which downloads a prebuilt wheel for CUDA 12, torch 2.5
  and Python 3.11.
- GroundingDINO and SAM 2 at fixed commits. They are built with
  `--no-build-isolation`, so their CUDA extensions compile against the image's
  torch instead of the newest torch on PyPI.
- `transformers` 5.17.0, with `tokenizers`, `huggingface-hub`, `hf-xet` and
  `safetensors` pinned beside it. It has no published advisories (every 4.x
  release has some that are fixed only in 5.x) and is the last release that
  needs nothing newer than torch 2.5.1: 5.18 adds a torch 2.6 call to its device
  helper, 5.19 makes that call on import, and 5.20 and later require torch 2.6.
  GroundingDINO's own BERT wrapper calls methods that transformers 5 removed,
  so the server gives GroundingDINO an equivalent text encoder
  (`app/gdino_text_encoder.py`). Florence-2 runs on transformers' own
  implementation, so no code from a model repository runs in the server.

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
alone and reported unless `--force` is given.

Two models come from the Hugging Face hub on first load, each at a fixed
repository commit (`app/hub_pins.py`), and are cached in the
`huggingface_cache` volume:

- BERT base uncased (`google-bert/bert-base-uncased`), GroundingDINO's text
  encoder. Its config names `bert-base-uncased`, which GroundingDINO would load
  from the moving `main` branch; the server loads a snapshot of the pinned
  commit instead (only `config.json`, `model.safetensors`, `tokenizer.json`,
  `tokenizer_config.json` and `vocab.txt`).
- Florence-2 (`FLORENCE_MODEL_ID`), loaded with transformers' own Florence-2
  implementation from a repository in its format: Microsoft's weights converted
  by Hugging Face, with configs and tokenizer files and no Python code.
  `florence-community/Florence-2-large` (the default) and
  `florence-community/Florence-2-base` have pinned commits. Microsoft's own
  repositories (`microsoft/Florence-2-large`, `-base`) are made for code that
  runs from the repository (`trust_remote_code`), which this server does not
  run; their ids load the converted repositories instead. For any other model
  id, set `FLORENCE_MODEL_REVISION` to a full 40-character commit id; the server
  refuses to load it otherwise, and refuses branch or tag names, which can
  move. A model id that is a local directory needs no revision. The server also
  refuses a checkpoint that leaves any Florence-2 weight unset, such as one
  made for the repository code.

Once both are cached, the server also starts with `HF_HUB_OFFLINE=1`.

## Authentication

Every endpoint, `GET /v1/health` included, requires a bearer token (the health
response names the GPU, its memory use, the loaded models and the uptime):

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
configured). Liveness probes send the token like any other client. `WS /v1/stream` takes the same `Authorization` header; browsers,
which cannot set headers on a WebSocket, offer the token as a subprotocol pair
instead: `new WebSocket(url, ["bearer", token])`. Tokens in query strings are
not accepted.

```bash
curl -H "Authorization: Bearer $CV_SERVER_API_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"image": "<BASE64_JPEG>", "prompt": "chair ."}' \
  http://localhost:8000/v1/detect
```

## TLS

TLS is on when both `SSL_CERTFILE` and `SSL_KEYFILE` point at existing files.
The container then serves HTTPS only, on `:443`, and starts no plain-HTTP
listener, so tokens and images never cross the network unencrypted. TLS is off
when neither file exists; only then does it serve plain HTTP, on `:8000` (use
that only on a trusted network or behind a TLS-terminating proxy). If only one
of the two files exists the container refuses to start, and with
`CV_SERVER_REQUIRE_TLS=1` it also refuses to start without TLS.

With `docker compose`, put the files in `cv-server/certs/` (mounted read-only at
`/app/certs`) and set their container paths in `.env`:

```bash
CV_SERVER_SSL_CERTFILE=/app/certs/<your-host>.crt
CV_SERVER_SSL_KEYFILE=/app/certs/<your-host>.key
```

They default to `/app/certs/server.crt` and `/app/certs/server.key`. Keep host
names and key material out of the repository; `certs/` and `.env` are gitignored.

## Environment Variables

- `CV_SERVER_API_TOKEN` (required): accepted bearer token(s), comma-separated
- `SSL_CERTFILE`, `SSL_KEYFILE`: TLS certificate and key paths in the container
  (compose sets them from `CV_SERVER_SSL_CERTFILE` / `CV_SERVER_SSL_KEYFILE`)
- `CV_SERVER_REQUIRE_TLS` (default: `0`): `1` refuses to start without a certificate and key
- `CV_SERVER_FETCH_WEIGHTS` (default: `1`): fetch/verify model files on container start
- `CV_SERVER_DEVICE` (default: `cuda`)
- `GDINO_CONFIG_PATH`
- `GDINO_WEIGHTS_PATH`
- `SAM2_CONFIG_PATH`
- `SAM2_WEIGHTS_PATH`
- `FLORENCE_MODEL_ID` (default: `florence-community/Florence-2-large`)
- `FLORENCE_MODEL_REVISION`: commit of `FLORENCE_MODEL_ID` to load; required for
  model ids without a pinned commit (see [Model Files](#model-files))
- `BOX_THRESHOLD`
- `TEXT_THRESHOLD`
- `GLTF_UPLOAD_DIR` (default: `uploads/gltf`)
- `CV_SERVER_MAX_REQUEST_BYTES` (default: `33554432`, 32 MiB): largest HTTP request
  body (base64 images, glTF uploads); larger requests get `413`
- `CV_SERVER_MAX_WS_MESSAGE_BYTES` (default: `16777216`, 16 MiB): largest
  WebSocket message on `/v1/stream`

## Endpoints

- `POST /v1/detect`
- `POST /v1/detect/batch`
- `POST /v1/segment`
- `POST /v1/florence`
- `POST /v1/gltf`
- `GET /v1/health`
- `WS /v1/stream`

### Boxes

Every box the API returns has the same format: `bboxes` from `/v1/detect`,
`/v1/detect/batch` and `/v1/florence`, `bbox` in each `/v1/segment` annotation,
and the same results on `WS /v1/stream`.

- `[x1, y1, x2, y2]`: the top-left corner, then the bottom-right corner, so
  `x1 <= x2` and `y1 <= y2`.
- The origin is the top-left corner of the image; x points right, y points down.
- Coordinates are fractions of the image size, from 0 to 1: x of the width, y of
  the height.

```json
{"bboxes": [[0.15, 0.30, 0.35, 0.70]], "labels": ["shelf"], "scores": [0.81]}
```

On a 1280x1024 image this shelf spans pixels 192 to 448 across and 307.2 to
716.8 down: multiply x by the width and y by the height of the image you sent.
Fractions stay the same when `max_side` makes the server shrink the image.
`/v1/segment` also returns `image_size`, the `[height, width]` of the image its
RLE masks cover (the shrunk one when `max_side` applied).

This is the format the Quest app reads (`BoundingBoxUtils.TryNormalize` in
`Assets/CompanionScan/Core/BoundingBoxUtils.cs`) and the web client in `web/`
expects. GroundingDINO itself outputs boxes as `(cx, cy, w, h)`, centre and size;
`app/utils/boxes.py` converts them before they leave the server.

### glTF Upload

`POST /v1/gltf` accepts multipart form data:

- `gltf` (file)
- `bin` (file, optional)
- `extras` (field, optional)

The whole request is capped at `CV_SERVER_MAX_REQUEST_BYTES` (32 MiB by
default). A larger `Content-Length` is refused with `413` before anything is
read, and a body sent without one is cut off with `413` as soon as it passes
the cap, so nothing is written to `GLTF_UPLOAD_DIR`.

The Quest app triggers this automatically when the user presses the **Y button**
on the left controller (Save & Export). See the main [README](../README.md) for
the full controller button mapping.

## Optional Request Fields

All image endpoints accept `max_side` to let the client request server-side
resizing. Boxes are fractions of the image size either way (see [Boxes](#boxes)).

## Testing

```bash
pytest
```

The smoke tests skip automatically if model weights are not present.
`tests/test_box_contract.py` and `tests/test_boxes.py` check the box format with
stand-in models, so they need neither weights nor a GPU.

## Load Testing

```bash
pip install -r load/requirements.txt
export CV_SERVER_API_TOKEN=<token>   # sent as the bearer token
locust -f load/locustfile.py --host http://localhost:8000 --image-b64 <BASE64_JPEG>
```

## Publishing

`.github/workflows/cv-server-publish.yml` builds this directory and pushes
`ghcr.io/<owner>/ci-xr-scan-cv-server:<number>`, authenticating with the
workflow's `GITHUB_TOKEN` only. Run it from the default branch:

```bash
gh workflow run cv-server-publish.yml            # tag = the workflow run number
gh workflow run cv-server-publish.yml -f tag=12  # or a numeric tag of your choice
```

An existing tag is never overwritten. The run prints the image digest and the
`<image>:<number>@sha256:<digest>` reference to pin in CI-Marketplace. The image
contains neither model files nor a token: give the container
`CV_SERVER_API_TOKEN`, and volumes on `/app/configs` and `/app/checkpoints` so
the model files are downloaded only once.
