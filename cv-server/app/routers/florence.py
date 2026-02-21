from fastapi import APIRouter, HTTPException

from app.schemas.florence import FlorenceRequest, FlorenceResponse
from app.pipelines.florence2 import run_florence2

router = APIRouter()


@router.post("/florence", response_model=FlorenceResponse)
async def florence(request: FlorenceRequest) -> FlorenceResponse:
    try:
        return run_florence2(request)
    except Exception as exc:  # pragma: no cover - surfaced as HTTP error
        raise HTTPException(status_code=500, detail=str(exc)) from exc
