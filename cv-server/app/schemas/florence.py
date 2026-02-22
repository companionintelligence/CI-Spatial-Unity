from typing import Any, Dict, Optional

from pydantic import BaseModel, Field


class FlorenceRequest(BaseModel):
    image: str = Field(..., description="Base64-encoded JPEG")
    task: str = Field(..., description="Florence-2 task prompt, e.g. <CAPTION>")
    text_input: str = Field("")
    max_side: Optional[int] = Field(
        None, description="Optional max image side for server-side resize"
    )


class FlorenceResponse(BaseModel):
    task: str
    results: Dict[str, Any]
    text: Optional[str] = None
