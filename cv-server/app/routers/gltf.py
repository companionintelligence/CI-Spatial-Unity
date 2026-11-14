from typing import Optional

from fastapi import APIRouter, File, Form, HTTPException, Response, UploadFile
from starlette import status
from starlette.concurrency import run_in_threadpool

from app import gltf_store
from app.config import settings

router = APIRouter()


@router.post("/gltf")
async def upload_gltf(
    gltf: UploadFile = File(...),
    bin: Optional[UploadFile] = File(None),
    extras: Optional[str] = Form(None),
) -> dict:
    """Store a glTF upload under a new opaque ID (app/gltf_store.py)."""
    gltf_bytes = await gltf.read()
    bin_bytes = await bin.read() if bin is not None else None
    stored = await run_in_threadpool(
        gltf_store.save_upload, settings.gltf_upload_dir, gltf_bytes, bin_bytes, extras
    )
    return {
        "ok": True,
        "id": stored.upload_id,
        "gltf_path": stored.gltf_path,
        "bin_path": stored.bin_path,
        "extras_path": stored.extras_path,
    }


@router.delete("/gltf/{upload_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_gltf(upload_id: str) -> Response:
    """Delete an upload's files; 404 when the ID names no upload."""
    if not gltf_store.delete_upload(settings.gltf_upload_dir, upload_id):
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Upload not found.")
    return Response(status_code=status.HTTP_204_NO_CONTENT)
