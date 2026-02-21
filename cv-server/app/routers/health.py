from fastapi import APIRouter

from app.utils.gpu_monitor import get_gpu_status

router = APIRouter()


@router.get("/health")
async def health() -> dict:
    return get_gpu_status()
