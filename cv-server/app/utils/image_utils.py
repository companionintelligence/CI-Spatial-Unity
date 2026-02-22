import base64
from io import BytesIO

from PIL import Image


def decode_base64_image(image_b64: str, max_side: int | None = None) -> Image.Image:
    image_bytes = base64.b64decode(image_b64)
    image = Image.open(BytesIO(image_bytes)).convert("RGB")
    if max_side is None or max_side <= 0:
        return image

    width, height = image.size
    longest_side = max(width, height)
    if longest_side <= max_side:
        return image

    scale = max_side / float(longest_side)
    new_size = (int(width * scale), int(height * scale))
    return image.resize(new_size, resample=Image.BILINEAR)
