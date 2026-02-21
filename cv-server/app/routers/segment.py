from fastapi import APIRouter, HTTPException

from app.schemas.segment import SegmentRequest, SegmentResponse
from app.pipelines.grounded_sam2 import run_grounded_sam2

router = APIRouter()


@router.post("/segment", response_model=SegmentResponse)
async def segment(request: SegmentRequest) -> SegmentResponse:
    try:
        return run_grounded_sam2(request)
    except Exception as exc:  # pragma: no cover - surfaced as HTTP error
        raise HTTPException(status_code=500, detail=str(exc)) from exc
