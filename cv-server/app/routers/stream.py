import json

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from app.pipelines.florence2 import run_florence2
from app.pipelines.grounded_sam2 import run_grounded_sam2
from app.pipelines.grounding_dino import run_grounding_dino
from app.schemas.detect import DetectRequest
from app.schemas.florence import FlorenceRequest
from app.schemas.segment import SegmentRequest

router = APIRouter()


@router.websocket("/stream")
async def stream(websocket: WebSocket) -> None:
    await websocket.accept()
    try:
        while True:
            payload = await websocket.receive_text()
            message = json.loads(payload)
            mode = message.get("mode", "detect")
            data = message.get("data", {})

            if mode == "detect":
                request = DetectRequest(**data)
                result = run_grounding_dino(request)
            elif mode == "segment":
                request = SegmentRequest(**data)
                result = run_grounded_sam2(request)
            elif mode == "florence":
                request = FlorenceRequest(**data)
                result = run_florence2(request)
            else:
                await websocket.send_text(
                    json.dumps({"error": f"Unsupported mode: {mode}"})
                )
                continue

            await websocket.send_text(result.model_dump_json())
    except WebSocketDisconnect:
        return
