#!/usr/bin/env python3
"""Render first-party synthetic room photos for the Companion Scan model tests.

The CV model tests need indoor scenes (closets, pantries, laundry rooms, ...)
to push through Florence-2, Grounding DINO and SAM 2. Earlier revisions used
photographs copied from the web whose licence was unknown, so they were
removed. This script draws replacement scenes procedurally with Pillow only:
no third-party images, textures, fonts or models are involved, so the output
is first-party work covered by this repository's LICENSE.

The output is deterministic for a given Pillow version (every random choice
comes from a seeded ``random.Random``), so the committed JPEGs can be
regenerated at any time:

    python tools/generate_synthetic_fixtures.py            # default targets
    python tools/generate_synthetic_fixtures.py --out DIR  # one directory

Default targets:
    test/                                  every scene (external QA fixtures)
    Assets/CompanionScan/Tests/Fixtures/   the scenes PerImageModelTests uses

Unity needs a .meta file next to each new file under Assets/; Unity creates
it on the next import if it is missing.
"""

from __future__ import annotations

import argparse
import random
from pathlib import Path
from typing import Callable, Dict, List, Sequence, Tuple

from PIL import Image, ImageDraw, ImageFilter

W, H = 1280, 1024  # Quest 3 passthrough camera resolution used by the tests
JPEG_COMMENT = (
    b"Synthetic test fixture rendered by tools/generate_synthetic_fixtures.py "
    b"(Companion Scan). First-party; contains no third-party imagery."
)

Color = Tuple[int, int, int]
Box = Tuple[float, float, float, float]

REPO_ROOT = Path(__file__).resolve().parent.parent
UNITY_FIXTURES = REPO_ROOT / "Assets" / "CompanionScan" / "Tests" / "Fixtures"
EXTERNAL_FIXTURES = REPO_ROOT / "test"


# ---------------------------------------------------------------------------
# Small drawing helpers
# ---------------------------------------------------------------------------

def shade(c: Color, f: float) -> Color:
    """Scale a colour towards black (f < 1) or white (f > 1)."""
    if f <= 1.0:
        return tuple(max(0, min(255, int(v * f))) for v in c)  # type: ignore[return-value]
    return tuple(max(0, min(255, int(v + (255 - v) * (f - 1.0)))) for v in c)  # type: ignore[return-value]


def mix(a: Color, b: Color, t: float) -> Color:
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))  # type: ignore[return-value]


class Canvas:
    """RGB image plus an RGBA overlay for soft shadows and translucency."""

    def __init__(self, rng: random.Random) -> None:
        self.rng = rng
        self.img = Image.new("RGB", (W, H), (200, 200, 200))
        self.d = ImageDraw.Draw(self.img)

    # -- room shell ---------------------------------------------------------
    def wall(self, top: Color, bottom: Color, horizon: int) -> None:
        for y in range(horizon):
            self.d.line([(0, y), (W, y)], fill=mix(top, bottom, y / max(1, horizon)))

    def floor(self, near: Color, far: Color, horizon: int, planks: bool = True) -> None:
        for y in range(horizon, H):
            t = (y - horizon) / max(1, H - horizon)
            self.d.line([(0, y), (W, y)], fill=mix(far, near, t))
        if planks:
            vx = W / 2
            for i in range(-14, 15):
                x_far = vx + i * 40
                x_near = vx + i * 150
                self.d.line([(x_far, horizon), (x_near, H)], fill=shade(far, 0.86), width=2)
        # contact shadow where wall meets floor
        self.soft_rect((0, horizon - 6, W, horizon + 18), (0, 0, 0), 70, blur=10)

    def baseboard(self, horizon: int, color: Color = (236, 233, 226)) -> None:
        self.d.rectangle([0, horizon - 26, W, horizon], fill=color)
        self.d.line([(0, horizon - 26), (W, horizon - 26)], fill=shade(color, 0.8), width=2)

    def window(self, box: Box, frame: Color = (245, 244, 240), sky: Color = (190, 214, 236)) -> None:
        x0, y0, x1, y1 = box
        self.d.rectangle([x0 - 14, y0 - 14, x1 + 14, y1 + 14], fill=frame)
        for y in range(int(y0), int(y1)):
            t = (y - y0) / max(1.0, y1 - y0)
            self.d.line([(x0, y), (x1, y)], fill=mix(sky, (236, 242, 246), t))
        mx = (x0 + x1) / 2
        my = (y0 + y1) / 2
        self.d.rectangle([mx - 6, y0, mx + 6, y1], fill=frame)
        self.d.rectangle([x0, my - 6, x1, my + 6], fill=frame)
        self.soft_rect((x0 - 14, y1 + 14, x1 + 14, y1 + 30), (0, 0, 0), 60, blur=8)

    def frame_picture(self, box: Box, colors: Sequence[Color]) -> None:
        x0, y0, x1, y1 = box
        self.soft_rect((x0 + 8, y0 + 10, x1 + 12, y1 + 14), (0, 0, 0), 70, blur=8)
        self.d.rectangle([x0, y0, x1, y1], fill=(60, 52, 44))
        self.d.rectangle([x0 + 14, y0 + 14, x1 - 14, y1 - 14], fill=(242, 240, 234))
        ix0, iy0, ix1, iy1 = x0 + 34, y0 + 34, x1 - 34, y1 - 34
        bands = len(colors)
        for i, c in enumerate(colors):
            yy0 = iy0 + (iy1 - iy0) * i / bands
            yy1 = iy0 + (iy1 - iy0) * (i + 1) / bands
            self.d.rectangle([ix0, yy0, ix1, yy1], fill=c)

    # -- translucent overlays -------------------------------------------------
    def soft_rect(self, box: Box, color: Color, alpha: int, blur: float = 6) -> None:
        overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(overlay).rectangle(box, fill=color + (alpha,))
        overlay = overlay.filter(ImageFilter.GaussianBlur(blur))
        self.img.paste(overlay, (0, 0), overlay)

    def soft_ellipse(self, box: Box, color: Color, alpha: int, blur: float = 10) -> None:
        overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(overlay).ellipse(box, fill=color + (alpha,))
        overlay = overlay.filter(ImageFilter.GaussianBlur(blur))
        self.img.paste(overlay, (0, 0), overlay)

    def glass(self, box: Box, radius: int, tint: Color = (225, 235, 238), alpha: int = 90) -> None:
        overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        od = ImageDraw.Draw(overlay)
        od.rounded_rectangle(box, radius=radius, fill=tint + (alpha,), outline=(255, 255, 255, 150), width=2)
        x0, y0, x1, y1 = box
        od.rectangle([x0 + (x1 - x0) * 0.16, y0 + 8, x0 + (x1 - x0) * 0.24, y1 - 10], fill=(255, 255, 255, 110))
        self.img.paste(overlay, (0, 0), overlay)

    # -- furniture and objects ----------------------------------------------
    def cuboid(self, x: float, y: float, w: float, h: float, depth: float, color: Color) -> None:
        """Box seen slightly from above and from the left; (x, y) is the front-top-left corner."""
        dx, dy = depth * 0.55, depth * 0.45
        self.soft_rect((x + 6, y + h - 6, x + w + dx + 10, y + h + 12), (0, 0, 0), 80, blur=7)
        self.d.polygon([(x, y), (x + dx, y - dy), (x + w + dx, y - dy), (x + w, y)], fill=shade(color, 1.18))
        self.d.polygon([(x + w, y), (x + w + dx, y - dy), (x + w + dx, y + h - dy), (x + w, y + h)], fill=shade(color, 0.72))
        self.d.rectangle([x, y, x + w, y + h], fill=color)
        self.d.line([(x, y), (x + w, y)], fill=shade(color, 1.3), width=2)

    def shelf(self, x0: float, x1: float, y: float, color: Color, thickness: int = 18) -> None:
        self.soft_rect((x0, y + thickness, x1, y + thickness + 26), (0, 0, 0), 90, blur=9)
        self.d.polygon([(x0, y), (x0 + 30, y - 22), (x1 - 30, y - 22), (x1, y)], fill=shade(color, 1.15))
        self.d.rectangle([x0, y, x1, y + thickness], fill=color)
        self.d.line([(x0, y), (x1, y)], fill=shade(color, 1.35), width=2)

    def jar(self, x: float, base_y: float, w: float, h: float, contents: Color, lid: Color, fill: float) -> None:
        top = base_y - h
        self.soft_ellipse((x - 4, base_y - 10, x + w + 10, base_y + 8), (0, 0, 0), 90, blur=5)
        level = base_y - h * fill
        self.d.rounded_rectangle([x + 4, level, x + w - 4, base_y - 4], radius=10, fill=contents)
        self.glass((x, top + 14, x + w, base_y), radius=14)
        self.d.rounded_rectangle([x + 3, top, x + w - 3, top + 20], radius=5, fill=lid)
        self.d.line([(x + 6, top + 4), (x + w - 6, top + 4)], fill=shade(lid, 1.3), width=2)

    def can(self, x: float, base_y: float, w: float, h: float, body: Color, band: Color) -> None:
        top = base_y - h
        self.soft_ellipse((x - 2, base_y - 8, x + w + 8, base_y + 6), (0, 0, 0), 90, blur=5)
        self.d.rectangle([x, top + 6, x + w, base_y - 6], fill=body)
        self.d.rectangle([x, top + h * 0.35, x + w, top + h * 0.7], fill=band)
        self.d.ellipse([x, top, x + w, top + 12], fill=(205, 205, 210), outline=(150, 150, 155))
        self.d.ellipse([x, base_y - 12, x + w, base_y], fill=shade(body, 0.8))
        self.d.line([(x + w * 0.2, top + 10), (x + w * 0.2, base_y - 10)], fill=shade(body, 1.4), width=3)

    def cereal_box(self, x: float, base_y: float, w: float, h: float, color: Color, stripe: Color) -> None:
        self.cuboid(x, base_y - h, w, h, 34, color)
        self.d.rectangle([x + 8, base_y - h * 0.68, x + w - 8, base_y - h * 0.4], fill=stripe)
        self.d.ellipse([x + w * 0.3, base_y - h * 0.9, x + w * 0.7, base_y - h * 0.72], fill=shade(stripe, 1.3))

    def basket(self, x: float, base_y: float, w: float, h: float, color: Color) -> None:
        top = base_y - h
        self.soft_rect((x + 6, base_y - 6, x + w + 6, base_y + 12), (0, 0, 0), 90, blur=7)
        self.d.polygon([(x, top), (x + w, top), (x + w - 12, base_y), (x + 12, base_y)], fill=color)
        for i in range(1, 6):
            yy = top + h * i / 6
            self.d.line([(x + 2 * i, yy), (x + w - 2 * i, yy)], fill=shade(color, 0.75), width=3)
        for i in range(1, 9):
            xx = x + w * i / 9
            self.d.line([(xx, top), (xx + (W / 2 - xx) * 0.01, base_y)], fill=shade(color, 0.82), width=2)
        self.d.rectangle([x - 4, top - 8, x + w + 4, top + 4], fill=shade(color, 1.15))

    def folded_stack(self, x: float, base_y: float, w: float, colors: Sequence[Color]) -> None:
        y = base_y
        for c in colors:
            hh = 26
            self.d.rounded_rectangle([x, y - hh, x + w, y], radius=10, fill=c, outline=shade(c, 0.75))
            self.d.line([(x + 10, y - hh / 2), (x + w - 10, y - hh / 2)], fill=shade(c, 0.85), width=2)
            y -= hh - 2

    def hanging_shirt(self, cx: float, rod_y: float, color: Color, length: float) -> None:
        self.d.arc([cx - 30, rod_y - 4, cx + 30, rod_y + 40], start=200, end=340, fill=(70, 70, 75), width=4)
        self.d.line([(cx, rod_y - 18), (cx, rod_y + 2)], fill=(70, 70, 75), width=4)
        top = rod_y + 22
        body = [(cx - 44, top), (cx + 44, top), (cx + 54, top + 46), (cx + 40, top + 56),
                (cx + 38, top + length), (cx - 38, top + length), (cx - 40, top + 56), (cx - 54, top + 46)]
        self.soft_rect((cx - 30, top + 10, cx + 60, top + length + 10), (0, 0, 0), 60, blur=10)
        self.d.polygon(body, fill=color, outline=shade(color, 0.7))
        self.d.line([(cx, top + 4), (cx, top + length)], fill=shade(color, 0.85), width=2)
        self.d.polygon([(cx - 12, top), (cx + 12, top), (cx, top + 18)], fill=shade(color, 0.75))

    def bottle(self, x: float, base_y: float, w: float, h: float, color: Color, cap: Color) -> None:
        top = base_y - h
        self.soft_ellipse((x - 4, base_y - 10, x + w + 10, base_y + 8), (0, 0, 0), 90, blur=5)
        self.d.rounded_rectangle([x, top + h * 0.22, x + w, base_y], radius=12, fill=color)
        self.d.polygon([(x + w * 0.2, top + h * 0.24), (x + w * 0.35, top + 10), (x + w * 0.65, top + 10), (x + w * 0.8, top + h * 0.24)], fill=color)
        self.d.rectangle([x + w * 0.33, top, x + w * 0.67, top + 14], fill=cap)
        self.d.rectangle([x + 8, top + h * 0.45, x + w - 8, top + h * 0.75], fill=(245, 245, 240))
        self.d.line([(x + w * 0.18, top + h * 0.3), (x + w * 0.18, base_y - 12)], fill=shade(color, 1.4), width=3)

    def appliance(self, x: float, y: float, w: float, h: float, door: bool = True) -> None:
        body = (232, 234, 236)
        self.cuboid(x, y, w, h, 60, body)
        self.d.rectangle([x, y, x + w, y + h * 0.16], fill=(214, 217, 220))
        for i in range(3):
            self.d.ellipse([x + 30 + i * 46, y + 22, x + 58 + i * 46, y + 50], fill=(150, 154, 160))
        self.d.rounded_rectangle([x + w - 120, y + 24, x + w - 24, y + 48], radius=6, fill=(40, 46, 52))
        if door:
            cx, cy = x + w / 2, y + h * 0.58
            r = w * 0.34
            self.d.ellipse([cx - r - 14, cy - r - 14, cx + r + 14, cy + r + 14], fill=(200, 204, 208))
            self.d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(70, 86, 98))
            self.d.ellipse([cx - r * 0.7, cy - r * 0.7, cx + r * 0.4, cy + r * 0.4], fill=(98, 118, 132))

    def lamp(self, cx: float, base_y: float, height: float, shade_color: Color) -> None:
        self.soft_ellipse((cx - 70, base_y - 10, cx + 80, base_y + 14), (0, 0, 0), 80, blur=8)
        self.d.ellipse([cx - 40, base_y - 16, cx + 40, base_y + 4], fill=(70, 66, 60))
        self.d.rectangle([cx - 5, base_y - height, cx + 5, base_y - 8], fill=(90, 86, 80))
        top = base_y - height - 110
        self.soft_ellipse((cx - 140, top - 60, cx + 140, top + 200), (255, 240, 200), 70, blur=40)
        self.d.polygon([(cx - 50, top), (cx + 50, top), (cx + 82, top + 110), (cx - 82, top + 110)], fill=shade_color)
        self.d.line([(cx - 82, top + 110), (cx + 82, top + 110)], fill=shade(shade_color, 0.8), width=3)

    def plant(self, cx: float, base_y: float, rng: random.Random) -> None:
        self.cuboid(cx - 50, base_y - 90, 100, 90, 28, (178, 104, 72))
        for _ in range(26):
            ang_x = rng.uniform(-150, 150)
            ang_y = rng.uniform(-260, -70)
            leaf = (rng.randint(40, 70), rng.randint(110, 150), rng.randint(50, 80))
            lx, ly = cx + ang_x, base_y - 90 + ang_y
            self.d.line([(cx, base_y - 92), (lx, ly)], fill=(64, 104, 58), width=4)
            self.d.ellipse([lx - 30, ly - 14, lx + 30, ly + 14], fill=leaf, outline=shade(leaf, 0.7))

    def rug(self, box: Box, color: Color, border: Color) -> None:
        x0, y0, x1, y1 = box
        inset = 60
        self.d.polygon([(x0 + inset, y0), (x1 - inset, y0), (x1, y1), (x0, y1)], fill=border)
        self.d.polygon([(x0 + inset + 26, y0 + 12), (x1 - inset - 26, y0 + 12), (x1 - 34, y1 - 18), (x0 + 34, y1 - 18)], fill=color)

    # -- finishing ------------------------------------------------------------
    def finish(self) -> Image.Image:
        img = self.img.filter(ImageFilter.GaussianBlur(0.7))
        # gentle vignette
        vig = Image.new("L", (W, H), 0)
        ImageDraw.Draw(vig).ellipse([-W * 0.15, -H * 0.2, W * 1.15, H * 1.2], fill=255)
        vig = vig.filter(ImageFilter.GaussianBlur(160))
        dark = Image.new("RGB", (W, H), (20, 18, 16))
        img = Image.composite(img, Image.blend(img, dark, 0.35), vig)
        # seeded low-frequency sensor grain
        gw, gh = W // 4, H // 4
        grain = Image.new("L", (gw, gh))
        grain.putdata([self.rng.randint(110, 146) for _ in range(gw * gh)])
        grain = grain.resize((W, H), Image.BICUBIC).convert("RGB")
        return Image.blend(img, grain, 0.06)


# ---------------------------------------------------------------------------
# Scenes
# ---------------------------------------------------------------------------

JAR_CONTENTS: List[Color] = [(233, 196, 106), (150, 92, 54), (246, 242, 232), (196, 120, 60), (120, 60, 40), (230, 220, 180)]
LIDS: List[Color] = [(40, 40, 42), (212, 212, 214), (156, 112, 70), (60, 90, 70)]
CLOTHES: List[Color] = [(52, 74, 110), (190, 60, 56), (232, 228, 218), (90, 120, 84), (40, 40, 44), (210, 170, 120), (120, 150, 190)]


def scene_pantry(c: Canvas) -> None:
    rng = c.rng
    horizon = 900
    c.wall((238, 232, 220), (214, 206, 192), horizon)
    c.floor((176, 140, 102), (150, 116, 82), horizon)
    c.baseboard(horizon)
    # shelving unit side panels
    c.d.rectangle([110, 60, 140, horizon], fill=(232, 228, 220))
    c.d.rectangle([1140, 60, 1170, horizon], fill=(214, 210, 202))
    for row, y in enumerate((250, 470, 690, 880)):
        c.shelf(140, 1140, y, (236, 232, 224))
        x = 170
        while x < 1080:
            kind = rng.choice(["jar", "jar", "can", "box", "basket"] if row < 3 else ["basket", "box"])
            if kind == "jar":
                w = rng.randint(70, 100)
                c.jar(x, y, w, rng.randint(120, 170), rng.choice(JAR_CONTENTS), rng.choice(LIDS), rng.uniform(0.45, 0.9))
            elif kind == "can":
                w = 64
                for i in range(rng.randint(2, 3)):
                    c.can(x + i * (w + 6), y, w, 96, rng.choice([(196, 52, 46), (230, 196, 60), (60, 120, 70)]), (245, 240, 230))
                w = (w + 6) * 3 - 6
            elif kind == "box":
                w = rng.randint(90, 130)
                c.cereal_box(x, y, w, rng.randint(150, 185), rng.choice([(206, 64, 52), (70, 110, 170), (232, 170, 48), (80, 140, 90)]), (250, 236, 200))
            else:
                w = rng.randint(170, 220)
                c.basket(x, y, w, rng.randint(90, 120), rng.choice([(176, 132, 84), (150, 110, 70), (214, 196, 160)]))
            x += w + rng.randint(18, 34)


def scene_food_storage(c: Canvas) -> None:
    rng = c.rng
    horizon = 430
    c.wall((226, 232, 236), (204, 212, 218), horizon)
    # tiled backsplash
    for y in range(80, horizon, 64):
        c.d.line([(0, y), (W, y)], fill=(188, 196, 204), width=2)
    for x in range(0, W, 128):
        c.d.line([(x, 80), (x, horizon)], fill=(188, 196, 204), width=2)
    # countertop
    c.d.rectangle([0, horizon, W, H], fill=(214, 206, 196))
    for y in range(horizon, H):
        t = (y - horizon) / (H - horizon)
        c.d.line([(0, y), (W, y)], fill=mix((196, 188, 178), (232, 226, 218), t))
    c.soft_rect((0, horizon - 4, W, horizon + 16), (0, 0, 0), 70, blur=8)
    # back row of tall jars on a riser, front row of stacked containers
    c.shelf(60, 1220, 520, (184, 150, 112), thickness=22)
    x = 90
    while x < 1140:
        w = rng.randint(90, 120)
        c.jar(x, 520, w, rng.randint(170, 220), rng.choice(JAR_CONTENTS), rng.choice(LIDS), rng.uniform(0.5, 0.92))
        x += w + rng.randint(16, 30)
    x = 80
    while x < 1120:
        w = rng.randint(150, 210)
        layers = rng.randint(1, 3)
        base = 930
        for i in range(layers):
            hh = rng.randint(90, 120)
            top = base - hh
            c.soft_rect((x + 8, base - 8, x + w + 10, base + 10), (0, 0, 0), 80, blur=6)
            c.d.rounded_rectangle([x + 6, top + 26, x + w - 6, base - 6], radius=10, fill=rng.choice(JAR_CONTENTS))
            c.glass((x, top + 18, x + w, base), radius=12, alpha=80)
            lid = rng.choice([(70, 130, 190), (220, 90, 80), (90, 170, 120), (240, 240, 236)])
            c.d.rounded_rectangle([x - 4, top, x + w + 4, top + 22], radius=8, fill=lid)
            base = top
        x += w + rng.randint(24, 40)


def scene_closet(c: Canvas) -> None:
    rng = c.rng
    horizon = 930
    c.wall((236, 232, 226), (218, 212, 204), horizon)
    c.floor((160, 124, 92), (136, 104, 74), horizon)
    c.baseboard(horizon)
    # upper shelf with folded clothes and storage boxes
    c.shelf(60, 1220, 230, (240, 238, 232))
    x = 90
    while x < 1150:
        if rng.random() < 0.55:
            w = rng.randint(140, 170)
            c.folded_stack(x, 230, w, [rng.choice(CLOTHES) for _ in range(rng.randint(3, 5))])
        else:
            w = rng.randint(150, 200)
            c.cuboid(x, 230 - 120, w, 120, 40, rng.choice([(228, 222, 210), (180, 150, 116), (120, 140, 160)]))
        x += w + rng.randint(20, 36)
    # hanging rod with shirts
    rod_y = 300
    c.d.rectangle([60, rod_y - 8, 1220, rod_y + 6], fill=(170, 172, 176))
    x = 120
    while x < 1160:
        c.hanging_shirt(x, rod_y, rng.choice(CLOTHES), rng.randint(260, 380))
        x += rng.randint(64, 86)
    # lower shelf with baskets and shoes
    c.shelf(60, 1220, 860, (240, 238, 232))
    x = 100
    while x < 1100:
        w = rng.randint(190, 240)
        c.basket(x, 860, w, rng.randint(110, 140), rng.choice([(176, 132, 84), (210, 200, 180), (120, 96, 70)]))
        x += w + rng.randint(30, 50)


def scene_laundry_room(c: Canvas) -> None:
    rng = c.rng
    horizon = 860
    c.wall((220, 230, 232), (198, 210, 214), horizon)
    c.floor((206, 206, 202), (178, 178, 174), horizon, planks=False)
    for i in range(0, W, 120):  # floor tiles
        c.d.line([(W / 2 + (i - W / 2) * 0.4, horizon), (i, H)], fill=(160, 160, 156), width=2)
    for y in (900, 950, 1010):
        c.d.line([(0, y), (W, y)], fill=(160, 160, 156), width=2)
    c.baseboard(horizon)
    # shelf with detergent bottles and baskets
    c.shelf(80, 1200, 280, (184, 150, 112))
    x = 110
    while x < 1120:
        if rng.random() < 0.6:
            w = rng.randint(80, 110)
            c.bottle(x, 280, w, rng.randint(150, 190), rng.choice([(60, 120, 200), (230, 120, 50), (90, 170, 120), (240, 240, 236)]), rng.choice([(240, 240, 240), (40, 40, 44)]))
        else:
            w = rng.randint(160, 200)
            c.basket(x, 280, w, rng.randint(90, 110), rng.choice([(214, 196, 160), (176, 132, 84)]))
        x += w + rng.randint(20, 34)
    # washer + dryer
    c.appliance(180, 450, 380, 410)
    c.appliance(640, 450, 380, 410)
    # laundry basket on the floor
    c.basket(1060, 1000, 190, 170, (226, 222, 214))
    c.folded_stack(1080, 836, 150, [rng.choice(CLOTHES) for _ in range(3)])


def scene_bedroom(c: Canvas) -> None:
    rng = c.rng
    horizon = 760
    c.wall((222, 214, 204), (204, 196, 186), horizon)
    c.floor((168, 128, 92), (140, 104, 72), horizon)
    c.baseboard(horizon)
    c.window((860, 150, 1160, 480))
    c.frame_picture((340, 150, 700, 360), [(120, 150, 180), (200, 180, 140), (110, 130, 100)])
    c.rug((160, 820, 1120, 1020), (156, 72, 64), (220, 206, 186))
    # bed
    c.d.rectangle([250, 380, 790, 560], fill=(96, 70, 52))  # headboard
    c.cuboid(250, 560, 540, 160, 120, (236, 234, 228))      # mattress
    c.d.polygon([(250, 640), (790, 640), (790, 720), (250, 720)], fill=(70, 100, 140))  # duvet
    c.d.polygon([(250, 620), (290, 600), (850, 600), (790, 640), (250, 640)], fill=(84, 116, 156))
    for px in (300, 540):
        c.d.rounded_rectangle([px, 520, px + 200, 600], radius=30, fill=(246, 244, 238), outline=(210, 206, 198))
    # nightstands and lamps
    for nx in (90, 830):
        c.cuboid(nx, 600, 130, 130, 50, (120, 88, 60))
        c.d.line([(nx + 10, 650), (nx + 120, 650)], fill=(90, 64, 44), width=3)
        c.lamp(nx + 65, 600, 70, (240, 226, 196))
    c.plant(1180, 900, rng)


def scene_living_room(c: Canvas) -> None:
    rng = c.rng
    horizon = 740
    c.wall((214, 222, 214), (194, 204, 196), horizon)
    c.floor((176, 138, 100), (150, 114, 80), horizon)
    c.baseboard(horizon)
    c.window((80, 140, 360, 470))
    c.frame_picture((520, 160, 860, 380), [(196, 120, 80), (230, 200, 150), (90, 110, 130)])
    c.rug((200, 800, 1180, 1020), (196, 180, 150), (120, 100, 80))
    # sofa
    sofa = (88, 100, 120)
    c.cuboid(430, 470, 520, 200, 70, shade(sofa, 0.9))      # back
    c.cuboid(400, 600, 580, 120, 90, sofa)                   # seat
    c.cuboid(370, 560, 80, 170, 80, shade(sofa, 0.85))       # left arm
    c.cuboid(930, 560, 80, 170, 80, shade(sofa, 0.85))       # right arm
    for cx in (470, 640, 790):
        c.d.rounded_rectangle([cx, 520, cx + 130, 610], radius=24, fill=rng.choice([(222, 180, 90), (230, 226, 216), (176, 84, 70)]))
    # coffee table with books and a mug
    c.cuboid(520, 820, 380, 60, 80, (130, 92, 62))
    c.cuboid(560, 790, 120, 30, 50, (40, 70, 110))
    c.cuboid(570, 764, 100, 26, 44, (200, 80, 64))
    c.can(780, 818, 46, 56, (236, 236, 232), (236, 236, 232))
    # tv unit
    c.cuboid(1010, 600, 240, 140, 60, (60, 56, 52))
    c.d.rectangle([1020, 420, 1240, 580], fill=(24, 26, 30))
    c.d.rectangle([1028, 428, 1232, 572], fill=(36, 42, 50))
    c.lamp(150, 760, 380, (238, 230, 214))
    c.plant(1180, 1000, rng)


SCENES: Dict[str, Callable[[Canvas], None]] = {
    "synthetic_pantry": scene_pantry,
    "synthetic_food_storage": scene_food_storage,
    "synthetic_closet": scene_closet,
    "synthetic_laundry_room": scene_laundry_room,
    "synthetic_bedroom": scene_bedroom,
    "synthetic_living_room": scene_living_room,
}

# Scenes copied into Assets/CompanionScan/Tests/Fixtures (see PerImageModelTests).
UNITY_SCENES = ("synthetic_pantry", "synthetic_food_storage", "synthetic_closet", "synthetic_laundry_room")


def render(name: str) -> Image.Image:
    seed = sum((i + 1) * ord(ch) for i, ch in enumerate(name))
    canvas = Canvas(random.Random(seed))
    SCENES[name](canvas)
    return canvas.finish()


def save(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "JPEG", quality=88, optimize=True, comment=JPEG_COMMENT)
    print(f"wrote {path.relative_to(REPO_ROOT) if path.is_relative_to(REPO_ROOT) else path}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, help="write every scene to this directory instead of the default targets")
    args = parser.parse_args()

    for name in SCENES:
        img = render(name)
        filename = f"{name}_{W}x{H}.jpg"
        if args.out:
            save(img, args.out / filename)
            continue
        save(img, EXTERNAL_FIXTURES / filename)
        if name in UNITY_SCENES:
            save(img, UNITY_FIXTURES / filename)


if __name__ == "__main__":
    main()
