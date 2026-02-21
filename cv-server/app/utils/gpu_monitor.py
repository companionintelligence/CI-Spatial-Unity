import time

import torch

from app.model_manager import ModelManager


_START_TIME = time.time()


def get_gpu_status() -> dict:
    if torch.cuda.is_available():
        device = torch.cuda.get_device_name(0)
        vram_total = torch.cuda.get_device_properties(0).total_memory // (1024 * 1024)
        vram_used = torch.cuda.memory_allocated(0) // (1024 * 1024)
    else:
        device = "cpu"
        vram_total = 0
        vram_used = 0

    mm = ModelManager.get_instance()
    models_loaded = []
    if mm.gdino_model is not None:
        models_loaded.append("grounding_dino")
    if mm.sam2_predictor is not None:
        models_loaded.append("sam2")
    if mm.florence_model is not None:
        models_loaded.append("florence2")

    return {
        "status": "ok",
        "gpu": device,
        "vram_used_mb": int(vram_used),
        "vram_total_mb": int(vram_total),
        "models_loaded": models_loaded,
        "uptime_seconds": int(time.time() - _START_TIME),
    }
