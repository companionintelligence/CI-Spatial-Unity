from typing import List

from pydantic import BaseModel, Field


class DetectRequest(BaseModel):
    image: str = Field(..., description="Base64-encoded JPEG")
    prompt: str = Field(..., description="Text prompt, e.g. 'chair . table .' ")
    box_threshold: float = Field(0.35, ge=0.0, le=1.0)
    text_threshold: float = Field(0.25, ge=0.0, le=1.0)


class DetectResponse(BaseModel):
    bboxes: List[List[float]]
    labels: List[str]
    scores: List[float]
