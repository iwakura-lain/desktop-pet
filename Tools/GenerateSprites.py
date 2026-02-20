#!/usr/bin/env python3
"""
Generate CC0 pixel-art anime cat-girl sprite sheets for desktop-pet.
Produces PNG sprite sheets for: idle, clicked, drag states (4 frames each).
Output: Assets/Sprites/catgirl_idle.png, catgirl_clicked.png, catgirl_drag.png
Each frame: 128x128px, RGBA transparent background.
"""

from PIL import Image, ImageDraw
import os, math

OUT_DIR = "/tmp/desktop-pet/Assets/Sprites"
os.makedirs(OUT_DIR, exist_ok=True)

W, H = 128, 128  # frame size
FRAMES = 4

# --- Colour palette (pixel-art anime) ---
SKIN      = (255, 220, 185, 255)
SKIN_S    = (235, 195, 160, 255)  # shadow
HAIR      = (90,  60, 130, 255)   # purple-ish
HAIR_H    = (130, 100, 180, 255)  # highlight
EYE_W     = (240, 240, 255, 255)
EYE_B     = (80,  50, 160, 255)
EYE_P     = (200, 100, 220, 255)
BLUSH     = (255, 160, 160, 160)
OUTFIT    = (100, 140, 220, 255)  # blue dress
OUTFIT_S  = (70,  100, 180, 255)
COLLAR    = (255, 255, 255, 255)
CAT_EAR   = (200, 150, 200, 255)
CAT_INNER = (255, 180, 200, 255)
TAIL_C    = (90,  60, 130, 255)
LINE      = (40,  20,  60, 255)
WHITE     = (255, 255, 255, 255)
CHEEK_R   = (255, 120, 120, 180)


def draw_frame(draw: ImageDraw.ImageDraw, ox: int, oy: int,
               bob: int = 0, tilt: float = 0.0,
               blink: bool = False, blush: bool = False,
               drag: bool = False):
    """Draw one 128x128 cat-girl frame at offset (ox, oy)."""

    # helper: shifted coords
    def p(x, y): return (ox + x, oy + y + bob)

    # ---- tail ----
    tail_pts = [
        p(96, 90), p(108, 80), p(118, 70), p(112, 60), p(100, 68)
    ]
    draw.polygon(tail_pts, fill=TAIL_C, outline=LINE)

    # ---- body / dress ----
    body = [p(44, 72), p(84, 72), p(90, 112), p(38, 112)]
    draw.polygon(body, fill=OUTFIT, outline=LINE)
    # shadow on dress
    shadow = [p(70, 72), p(84, 72), p(90, 112), p(76, 112)]
    draw.polygon(shadow, fill=OUTFIT_S)
    # collar
    draw.polygon([p(56, 72), p(72, 72), p(64, 82)], fill=COLLAR, outline=LINE)

    # ---- arms ----
    if drag:
        # arms raised
        draw.rounded_rectangle([p(30, 58), p(44, 80)], radius=6, fill=SKIN, outline=LINE)
        draw.rounded_rectangle([p(84, 58), p(98, 80)], radius=6, fill=SKIN, outline=LINE)
    else:
        draw.rounded_rectangle([p(32, 72), p(44, 96)], radius=6, fill=SKIN, outline=LINE)
        draw.rounded_rectangle([p(84, 72), p(96, 96)], radius=6, fill=SKIN, outline=LINE)

    # ---- head ----
    head_x0, head_y0 = ox + 38, oy + 22 + bob
    head_x1, head_y1 = ox + 90, oy + 70 + bob
    draw.ellipse([head_x0, head_y0, head_x1, head_y1], fill=SKIN, outline=LINE)
    # chin / face shadow
    draw.ellipse([head_x0+4, head_y0+28, head_x1-4, head_y1+4], fill=SKIN_S)

    # ---- cat ears ----
    ear_bob = bob
    # left ear
    draw.polygon([p(42, 24+ear_bob), p(34, 8+ear_bob), p(52, 18+ear_bob)],
                 fill=CAT_EAR, outline=LINE)
    draw.polygon([p(43, 22+ear_bob), p(37, 11+ear_bob), p(50, 19+ear_bob)],
                 fill=CAT_INNER)
    # right ear
    draw.polygon([p(86, 24+ear_bob), p(94, 8+ear_bob), p(76, 18+ear_bob)],
                 fill=CAT_EAR, outline=LINE)
    draw.polygon([p(85, 22+ear_bob), p(91, 11+ear_bob), p(78, 19+ear_bob)],
                 fill=CAT_INNER)

    # ---- hair ----
    # top
    draw.ellipse([p(36, 18), p(92, 52)], fill=HAIR, outline=LINE)
    # bangs
    draw.polygon([p(38, 38), p(46, 52), p(40, 54)], fill=HAIR, outline=LINE)
    draw.polygon([p(86, 38), p(78, 52), p(84, 54)], fill=HAIR, outline=LINE)
    draw.polygon([p(52, 35), p(60, 50), p(44, 50)], fill=HAIR)
    draw.polygon([p(76, 35), p(68, 50), p(84, 50)], fill=HAIR)
    # highlight
    draw.ellipse([p(50, 22), p(66, 32)], fill=HAIR_H)

    # ---- eyes ----
    if blink:
        # closed eyes (lines)
        draw.line([p(50, 47), p(58, 47)], fill=LINE, width=2)
        draw.line([p(70, 47), p(78, 47)], fill=LINE, width=2)
    else:
        # left eye
        draw.ellipse([p(48, 43), p(60, 53)], fill=EYE_W, outline=LINE)
        draw.ellipse([p(51, 45), p(57, 51)], fill=EYE_B)
        draw.ellipse([p(52, 46), p(56, 50)], fill=EYE_P)
        draw.ellipse([p(53, 46), p(55, 48)], fill=WHITE)
        # right eye
        draw.ellipse([p(68, 43), p(80, 53)], fill=EYE_W, outline=LINE)
        draw.ellipse([p(71, 45), p(77, 51)], fill=EYE_B)
        draw.ellipse([p(72, 46), p(76, 50)], fill=EYE_P)
        draw.ellipse([p(73, 46), p(75, 48)], fill=WHITE)

    # ---- blush ----
    if blush:
        draw.ellipse([p(44, 52), p(54, 58)], fill=CHEEK_R)
        draw.ellipse([p(74, 52), p(84, 58)], fill=CHEEK_R)

    # ---- mouth ----
    if blush:
        # surprised "o" mouth
        draw.ellipse([p(61, 56), p(67, 62)], fill=LINE)
    else:
        draw.arc([p(58, 56), p(70, 64)], start=0, end=180, fill=LINE, width=2)

    # ---- legs ----
    draw.rounded_rectangle([p(50, 112), p(60, 124)], radius=4, fill=SKIN, outline=LINE)
    draw.rounded_rectangle([p(68, 112), p(78, 124)], radius=4, fill=SKIN, outline=LINE)


def make_sheet(name: str, frame_configs: list):
    """Create a horizontal sprite sheet with len(frame_configs) frames."""
    sheet = Image.new("RGBA", (W * FRAMES, H), (0, 0, 0, 0))
    for i, cfg in enumerate(frame_configs):
        frame = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        draw = ImageDraw.Draw(frame)
        draw_frame(draw, 0, 0, **cfg)
        sheet.paste(frame, (i * W, 0))
    path = os.path.join(OUT_DIR, name)
    sheet.save(path)
    print(f"Saved: {path}  ({W*FRAMES}x{H})")


# ---- Idle: gentle bob up/down ----
make_sheet("catgirl_idle.png", [
    {"bob": 0,  "blush": False},
    {"bob": -2, "blush": False},
    {"bob": -3, "blush": False},
    {"bob": -1, "blush": False},
])

# ---- Clicked: surprise + blush ----
make_sheet("catgirl_clicked.png", [
    {"bob": 0,  "blush": True,  "blink": False},
    {"bob": -4, "blush": True,  "blink": False},
    {"bob": -4, "blush": True,  "blink": True},
    {"bob": -2, "blush": True,  "blink": False},
])

# ---- Drag: arms up, slight tilt ----
make_sheet("catgirl_drag.png", [
    {"bob": 0,  "drag": True, "blush": False},
    {"bob": -2, "drag": True, "blush": False},
    {"bob": -4, "drag": True, "blush": False},
    {"bob": -2, "drag": True, "blush": False},
])

print("All sprite sheets generated.")
