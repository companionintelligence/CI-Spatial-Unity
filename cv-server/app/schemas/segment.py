from typing import List, Optional

from pydantic import BaseModel, Field


class RLEMask(BaseModel):
    size: List[int]
    counts: str


class SegmentAnnotation(BaseModel):
    label: str
    score: float
    bbox: List[float] = Field(
        ...,
        description=(
            "[x1, y1, x2, y2]: top-left and bottom-right corners as fractions (0-1) "
            "of the image width (x) and height (y), origin top-left"
        ),
    )
    mask: Optional[RLEMask] = None


class SegmentRequest(BaseModel):
    image: str = Field(..., description="Base64-encoded JPEG")
    prompt: str = Field(..., description="Text prompt, e.g. 'couch . window .' ")
    box_threshold: float = Field(0.35, ge=0.0, le=1.0)
    text_threshold: float = Field(0.25, ge=0.0, le=1.0)
    grounding_model: str = Field("grounding_dino")
    return_masks: bool = Field(True)
    mask_format: str = Field("rle")
    max_side: Optional[int] = Field(
        None, description="Optional max image side for server-side resize"
    )


class SegmentResponse(BaseModel):
    annotations: List[SegmentAnnotation]
    image_size: List[int] = Field(
        ...,
        description="[height, width] in pixels of the image the masks were computed on (after max_side)",
    )
