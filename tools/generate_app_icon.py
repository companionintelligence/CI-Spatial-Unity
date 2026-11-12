#!/usr/bin/env python3
"""Render the first-party placeholder launcher icon for the Companion Scan APK.

Earlier revisions used web photographs of closets and pantries, whose licence
was unknown, as the Android launcher icons. They were removed, and this script
draws a replacement with Pillow primitives only: no third-party images, fonts
or logos are involved, so the output is first-party work covered by this
repository's LICENSE. Replace it with the product's real icon when there is
one, keeping the same path and .meta so the Player Settings references stay
valid.

The drawing is fully deterministic, so the committed PNG can be regenerated at
any time:

    python tools/generate_app_icon.py             # writes the default target
    python tools/generate_app_icon.py --out FILE  # writes FILE instead

Default target:
    Assets/CompanionScan/Icons/AppIcon.png  (Default Icon in Player Settings
                                             and in the Meta Quest build profile)

The motif is a scan viewfinder: four corner brackets, a horizontal scan line
and a centred target dot on a dark background that matches the splash screen
colour.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw
from PIL.PngImagePlugin import PngInfo

SIZE = 1024          # Unity scales the Default Icon down to every Android size
SUPERSAMPLE = 4      # draw large, then downsample for smooth edges
COMMENT = (
    "Placeholder launcher icon rendered by tools/generate_app_icon.py "
    "(Companion Scan). First-party; contains no third-party imagery."
)

BACKGROUND_TOP = (43, 39, 41)       # splash screen colour (0.137, 0.122, 0.125), lifted slightly
BACKGROUND_BOTTOM = (28, 25, 26)
ACCENT = (94, 214, 199)             # teal viewfinder
ACCENT_DIM = (94, 214, 199, 110)    # translucent scan line

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUT = REPO_ROOT / "Assets" / "CompanionScan" / "Icons" / "AppIcon.png"


def render() -> Image.Image:
    s = SIZE * SUPERSAMPLE
    img = Image.new("RGBA", (s, s))

    # Vertical gradient background (full bleed, so launcher masks of any shape work).
    grad = ImageDraw.Draw(img)
    for y in range(s):
        t = y / (s - 1)
        c = tuple(round(a + (b - a) * t) for a, b in zip(BACKGROUND_TOP, BACKGROUND_BOTTOM))
        grad.line([(0, y), (s, y)], fill=c + (255,))

    overlay = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)

    # Corner brackets inside the central safe area (about 66 % of the canvas),
    # so circular and squircle launcher masks do not clip them.
    margin = round(s * 0.22)
    arm = round(s * 0.17)
    width = round(s * 0.045)
    radius = width // 2
    lo, hi = margin, s - margin
    for (x, y, dx, dy) in ((lo, lo, 1, 1), (hi, lo, -1, 1), (lo, hi, 1, -1), (hi, hi, -1, -1)):
        # horizontal arm
        x0, x1 = sorted((x, x + dx * arm))
        y0, y1 = sorted((y, y + dy * width))
        d.rounded_rectangle([x0, y0, x1, y1], radius=radius, fill=ACCENT)
        # vertical arm
        x0, x1 = sorted((x, x + dx * width))
        y0, y1 = sorted((y, y + dy * arm))
        d.rounded_rectangle([x0, y0, x1, y1], radius=radius, fill=ACCENT)

    # Scan line across the frame.
    line_h = round(s * 0.012)
    cy = s // 2
    d.rounded_rectangle(
        [lo + width * 2, cy - line_h // 2, hi - width * 2, cy + line_h // 2],
        radius=line_h // 2,
        fill=ACCENT_DIM,
    )

    # Target dot with a ring.
    ring_r = round(s * 0.085)
    ring_w = round(s * 0.022)
    d.ellipse([cy - ring_r, cy - ring_r, cy + ring_r, cy + ring_r], outline=ACCENT, width=ring_w)
    dot_r = round(s * 0.032)
    d.ellipse([cy - dot_r, cy - dot_r, cy + dot_r, cy + dot_r], fill=ACCENT)

    img = Image.alpha_composite(img, overlay)
    return img.resize((SIZE, SIZE), Image.Resampling.LANCZOS).convert("RGB")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="output PNG path")
    args = parser.parse_args()

    args.out.parent.mkdir(parents=True, exist_ok=True)
    info = PngInfo()
    info.add_text("Comment", COMMENT)
    render().save(args.out, format="PNG", pnginfo=info, optimize=False, compress_level=9)
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
