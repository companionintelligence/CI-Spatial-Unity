from typing import List

from fastapi import APIRouter, HTTPException

from app.schemas.detect import DetectRequest, DetectResponse
from app.pipelines.grounding_dino import run_grounding_dino

router = APIRouter()


@router.post("/detect", response_model=DetectResponse)
async def detect(request: DetectRequest) -> DetectResponse:
    try:
        result = run_grounding_dino(request)
        return result
    except Exception as exc:  # pragma: no cover - surfaced as HTTP error
        raise HTTPException(status_code=500, detail=str(exc)) from exc


@router.post("/detect/batch", response_model=List[DetectResponse])
async def detect_batch(requests: List[DetectRequest]) -> List[DetectResponse]:
    try:
        return [run_grounding_dino(request) for request in requests]
    except Exception as exc:  # pragma: no cover - surfaced as HTTP error
        raise HTTPException(status_code=500, detail=str(exc)) from exc
