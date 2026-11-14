from fastapi import FastAPI

from app import gltf_store
from app.auth import BearerTokenMiddleware, require_configured_token
from app.config import settings
from app.limits import RequestSizeLimitMiddleware
from app.routers.detect import router as detect_router
from app.routers.health import router as health_router
from app.routers.segment import router as segment_router
from app.routers.florence import router as florence_router
from app.routers.stream import router as stream_router
from app.routers.gltf import router as gltf_router
from app.model_manager import ModelManager

app = FastAPI(title="CV Server", version="0.1.0")
# Request bodies over CV_SERVER_MAX_REQUEST_BYTES (default 32 MiB) get 413. Added first, so it
# runs inside the token check: requests without a valid token are refused before any body is read.
app.add_middleware(RequestSizeLimitMiddleware)
# Every route, /v1/health included, needs "Authorization: Bearer <CV_SERVER_API_TOKEN>".
app.add_middleware(BearerTokenMiddleware)

app.include_router(health_router, prefix="/v1")
app.include_router(detect_router, prefix="/v1")
app.include_router(segment_router, prefix="/v1")
app.include_router(florence_router, prefix="/v1")
app.include_router(stream_router, prefix="/v1")
app.include_router(gltf_router, prefix="/v1")


@app.on_event("startup")
def startup() -> None:
    require_configured_token()
    mm = ModelManager.get_instance()
    if settings.load_all_models:
        mm.load_grounding_dino()
        mm.load_sam2()
        mm.load_florence2()


@app.on_event("startup")
async def start_upload_retention() -> None:
    # With GLTF_UPLOAD_RETENTION_DAYS set, delete expired glTF uploads now and then hourly.
    await gltf_store.start_retention()


@app.on_event("shutdown")
def shutdown() -> None:
    ModelManager.get_instance().shutdown()


@app.on_event("shutdown")
async def stop_upload_retention() -> None:
    await gltf_store.stop_retention()
