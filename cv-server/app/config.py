import os

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Bearer token(s) required on every route, /v1/health included. Read only from
    # CV_SERVER_API_TOKEN; comma-separate several tokens to rotate without downtime.
    api_token: SecretStr = Field(
        default=SecretStr(""), validation_alias="CV_SERVER_API_TOKEN"
    )
    device: str = os.getenv("CV_SERVER_DEVICE", "cuda")
    gdino_config_path: str = os.getenv(
        "GDINO_CONFIG_PATH", "configs/GroundingDINO_SwinT_OGC.py"
    )
    gdino_weights_path: str = os.getenv(
        "GDINO_WEIGHTS_PATH", "checkpoints/groundingdino_swint_ogc.pth"
    )
    sam2_config_path: str = os.getenv(
        "SAM2_CONFIG_PATH", "configs/sam2.1/sam2.1_hiera_t.yaml"
    )
    sam2_weights_path: str = os.getenv(
        "SAM2_WEIGHTS_PATH", "checkpoints/sam2.1_hiera_tiny.pt"
    )
    # A repository in the format of transformers' own Florence-2 implementation. The
    # Microsoft ids load their converted repositories (app/hub_pins.py).
    florence_model_id: str = os.getenv(
        "FLORENCE_MODEL_ID", "florence-community/Florence-2-large"
    )
    # Commit of FLORENCE_MODEL_ID to load. Empty: the commit pinned in app/hub_pins.py,
    # which covers florence-community/Florence-2-large and -base; other ids need one.
    florence_model_revision: str = os.getenv("FLORENCE_MODEL_REVISION", "")
    gltf_upload_dir: str = os.getenv("GLTF_UPLOAD_DIR", "uploads/gltf")
    # Largest HTTP request body accepted, in bytes (base64 images, glTF uploads); 413 above it.
    max_request_bytes: int = int(os.getenv("CV_SERVER_MAX_REQUEST_BYTES", str(32 * 1024 * 1024)))
    load_all_models: bool = os.getenv("CV_SERVER_LOAD_ALL", "true").lower() == "true"
    default_box_threshold: float = float(os.getenv("BOX_THRESHOLD", "0.35"))
    default_text_threshold: float = float(os.getenv("TEXT_THRESHOLD", "0.25"))


settings = Settings()
