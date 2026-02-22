from typing import List, Optional

from pydantic import BaseModel, Field


class DetectRequest(BaseModel):
    image: str = Field(..., description="Base64-encoded JPEG")
    prompt: str = Field(..., description="Text prompt, e.g. 'chair . table .' ")
    box_threshold: float = Field(0.35, ge=0.0, le=1.0)
    text_threshold: float = Field(0.25, ge=0.0, le=1.0)
    max_side: Optional[int] = Field(
        None, description="Optional max image side for server-side resize"
    )


class DetectResponse(BaseModel):
    bboxes: List[List[float]]
    labels: List[str]
    scores: List[float]
