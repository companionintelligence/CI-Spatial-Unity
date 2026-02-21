from typing import Any, Dict, Optional

from pydantic import BaseModel, Field


class FlorenceRequest(BaseModel):
    image: str = Field(..., description="Base64-encoded JPEG")
    task: str = Field(..., description="Florence-2 task prompt, e.g. <CAPTION>")
    text_input: str = Field("")


class FlorenceResponse(BaseModel):
    task: str
    results: Dict[str, Any]
    text: Optional[str] = None
