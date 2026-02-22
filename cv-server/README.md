# CV Server

FastAPI server hosting Grounding DINO, SAM 2, and Florence-2 behind a single REST API.

## Quick Start

1) Place model checkpoints under `cv-server/checkpoints/` and configs under `cv-server/configs/`.
2) Build and run the container:

```bash
docker compose up --build
```

3) Health check:

```bash
curl http://localhost:8000/v1/health
```

## Environment Variables

- `CV_SERVER_DEVICE` (default: `cuda`)
- `GDINO_CONFIG_PATH`
- `GDINO_WEIGHTS_PATH`
- `SAM2_CONFIG_PATH`
- `SAM2_WEIGHTS_PATH`
- `FLORENCE_MODEL_ID`
- `BOX_THRESHOLD`
- `TEXT_THRESHOLD`

## Endpoints

- `POST /v1/detect`
- `POST /v1/detect/batch`
- `POST /v1/segment`
- `POST /v1/florence`
- `GET /v1/health`
- `WS /v1/stream`

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
locust -f load/locustfile.py --host http://localhost:8000 --image-b64 <BASE64_JPEG>
```
