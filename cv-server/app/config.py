import os

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Bearer token(s) required on every route except /v1/health. Read only from
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
    florence_model_id: str = os.getenv(
        "FLORENCE_MODEL_ID", "microsoft/Florence-2-large"
    )
    gltf_upload_dir: str = os.getenv("GLTF_UPLOAD_DIR", "uploads/gltf")
    load_all_models: bool = os.getenv("CV_SERVER_LOAD_ALL", "true").lower() == "true"
    default_box_threshold: float = float(os.getenv("BOX_THRESHOLD", "0.35"))
    default_text_threshold: float = float(os.getenv("TEXT_THRESHOLD", "0.25"))


settings = Settings()
