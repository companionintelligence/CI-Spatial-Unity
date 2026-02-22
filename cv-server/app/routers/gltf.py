import os
import time
import uuid
from typing import Optional

from fastapi import APIRouter, File, Form, UploadFile

from app.config import settings

router = APIRouter()


@router.post("/gltf")
async def upload_gltf(
    gltf: UploadFile = File(...),
    bin: Optional[UploadFile] = File(None),
    extras: Optional[str] = Form(None),
):
    os.makedirs(settings.gltf_upload_dir, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    uid = uuid.uuid4().hex[:8]
    base_name = f"spatial-map-{stamp}-{uid}"

    gltf_path = os.path.join(settings.gltf_upload_dir, base_name + ".gltf")
    with open(gltf_path, "wb") as f:
        f.write(await gltf.read())

    bin_path = None
    if bin is not None:
        bin_path = os.path.join(settings.gltf_upload_dir, base_name + ".bin")
        with open(bin_path, "wb") as f:
            f.write(await bin.read())

    extras_path = None
    if extras:
        extras_path = os.path.join(settings.gltf_upload_dir, base_name + ".extras.json")
        with open(extras_path, "w", encoding="utf-8") as f:
            f.write(extras)

    return {
        "ok": True,
        "gltf_path": gltf_path,
        "bin_path": bin_path,
        "extras_path": extras_path,
    }
