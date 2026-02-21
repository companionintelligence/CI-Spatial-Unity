from fastapi import APIRouter, HTTPException

from app.schemas.detect import DetectRequest, DetectResponse
from app.pipelines.grounding_dino import run_grounding_dino

router = APIRouter()


@router.post("/detect", response_model=DetectResponse)
async def detect(request: DetectRequest) -> DetectResponse:
    try:
        result = run_grounding_dino(
            request.image,
            request.prompt,
            request.box_threshold,
            request.text_threshold,
        )
        return result
    except Exception as exc:  # pragma: no cover - surfaced as HTTP error
        raise HTTPException(status_code=500, detail=str(exc)) from exc
