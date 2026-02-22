#!/usr/bin/env python3
"""
Resize and crop images to match Quest 3 passthrough camera resolution (1280×1024).

Usage:
    python prep_passthrough_images.py <input_dir_or_files...> [--output <dir>] [--quality 85]

Examples:
    # Process all images in a directory
    python prep_passthrough_images.py ~/photos --output ../tests/fixtures

    # Process specific files
    python prep_passthrough_images.py room.webp kitchen.jpg --output ../tests/fixtures

    # Pipe a single file to stdout as base64 (for curl testing)
    python prep_passthrough_images.py room.jpg --base64

Supports: .jpg, .jpeg, .png, .webp, .bmp, .tiff
Output: JPEG at the specified quality, named <stem>_1280x1024.jpg
"""

import argparse
import base64
import sys
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Pillow is required: pip install Pillow", file=sys.stderr)
    sys.exit(1)

# Quest 3 passthrough camera resolution
TARGET_WIDTH = 1280
TARGET_HEIGHT = 1024
TARGET_ASPECT = TARGET_WIDTH / TARGET_HEIGHT  # 1.25


def center_crop_and_resize(img: Image.Image) -> Image.Image:
    """
    Center-crop the image to the target aspect ratio, then resize to 1280×1024.
    This simulates what the Quest 3 passthrough camera frame would look like.
    """
    w, h = img.size
    src_aspect = w / h

    if src_aspect > TARGET_ASPECT:
        # Image is wider → crop width
        new_w = int(h * TARGET_ASPECT)
        offset = (w - new_w) // 2
        img = img.crop((offset, 0, offset + new_w, h))
    elif src_aspect < TARGET_ASPECT:
        # Image is taller → crop height
        new_h = int(w / TARGET_ASPECT)
        offset = (h - new_h) // 2
        img = img.crop((0, offset, w, offset + new_h))

    return img.resize((TARGET_WIDTH, TARGET_HEIGHT), Image.LANCZOS)


def process_image(path: Path, output_dir: Path, quality: int) -> Path:
    """Process a single image file and save the result."""
    img = Image.open(path).convert("RGB")
    result = center_crop_and_resize(img)

    out_name = f"{path.stem}_1280x1024.jpg"
    out_path = output_dir / out_name
    result.save(out_path, "JPEG", quality=quality)
    return out_path


def image_to_base64(path: Path) -> str:
    """Process and return as base64 string."""
    img = Image.open(path).convert("RGB")
    result = center_crop_and_resize(img)
    buf = BytesIO()
    result.save(buf, "JPEG", quality=85)
    return base64.b64encode(buf.getvalue()).decode("utf-8")


SUPPORTED_EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif"}


def collect_images(paths: list[str]) -> list[Path]:
    """Collect image files from supplied paths (files or directories)."""
    result = []
    for p_str in paths:
        p = Path(p_str)
        if p.is_dir():
            for ext in SUPPORTED_EXTENSIONS:
                result.extend(p.glob(f"*{ext}"))
                result.extend(p.glob(f"*{ext.upper()}"))
        elif p.is_file() and p.suffix.lower() in SUPPORTED_EXTENSIONS:
            result.append(p)
        else:
            print(f"Skipping: {p}", file=sys.stderr)
    return sorted(set(result))


def main():
    parser = argparse.ArgumentParser(
        description="Resize & crop images to Quest 3 passthrough camera resolution (1280×1024)"
    )
    parser.add_argument("inputs", nargs="+", help="Input files or directories")
    parser.add_argument(
        "--output", "-o",
        default=None,
        help="Output directory (default: <script_dir>/../tests/fixtures)"
    )
    parser.add_argument("--quality", "-q", type=int, default=85, help="JPEG quality (default: 85)")
    parser.add_argument(
        "--base64", action="store_true",
        help="Output base64 of the first image to stdout (for curl/API testing)"
    )
    args = parser.parse_args()

    images = collect_images(args.inputs)
    if not images:
        print("No supported images found.", file=sys.stderr)
        sys.exit(1)

    if args.base64:
        print(image_to_base64(images[0]))
        return

    output_dir = Path(args.output) if args.output else Path(__file__).parent.parent / "tests" / "fixtures"
    output_dir.mkdir(parents=True, exist_ok=True)

    for img_path in images:
        out = process_image(img_path, output_dir, args.quality)
        print(f"  {img_path.name} → {out}")

    print(f"\nProcessed {len(images)} image(s) → {output_dir}")


if __name__ == "__main__":
    main()
