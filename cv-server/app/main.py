from fastapi import FastAPI

from app.config import settings
from app.routers.detect import router as detect_router
from app.routers.health import router as health_router
from app.routers.segment import router as segment_router
from app.routers.florence import router as florence_router
from app.routers.stream import router as stream_router
from app.model_manager import ModelManager

app = FastAPI(title="CV Server", version="0.1.0")

app.include_router(health_router, prefix="/v1")
app.include_router(detect_router, prefix="/v1")
app.include_router(segment_router, prefix="/v1")
app.include_router(florence_router, prefix="/v1")
app.include_router(stream_router, prefix="/v1")


@app.on_event("startup")
def startup() -> None:
    mm = ModelManager.get_instance()
    if settings.load_all_models:
        mm.load_grounding_dino()
        mm.load_sam2()
        mm.load_florence2()


@app.on_event("shutdown")
def shutdown() -> None:
    ModelManager.get_instance().shutdown()
