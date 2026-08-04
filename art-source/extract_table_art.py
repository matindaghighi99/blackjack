#!/usr/bin/env python3
"""
Derives the game-table art directly from docs/screenshots/game-table.png.

That concept render is a 1024x1536 image of the finished table, so rather than trying
to redraw it procedurally we use it as the source of truth: the static furniture (rail,
chip tray, card shoe, felt, arc text, spade medallion) becomes the scene background, and
the dynamic pieces (cards, bet chip, action buttons) are lifted out as separate sprites
and re-composited live by Unity.

Regions occupied by dynamic content are inpainted with a per-row colour sampled from
clean parts of the same row, which works because the felt and rail are close to
horizontally uniform.

    python3 art-source/extract_table_art.py

Output:
    Assets/Art/Table/TableBackground.png    full-screen 1080x1920 plate
    Assets/Art/UI/chip_stack.png            bet chip stack
    Assets/Art/Cards/card_Back.png          gold filigree card back (replaces the drawn one)
    Assets/Art/UI/btn_action.b*.png         action button frame (nine-sliced)
    Assets/Art/UI/icon_hit|stand|double|split.png
"""

import os

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, "docs", "screenshots", "game-table.png")
TABLE_DIR = os.path.join(ROOT, "Assets", "Art", "Table")
CARDS_DIR = os.path.join(ROOT, "Assets", "Art", "Cards")
UI_DIR = os.path.join(ROOT, "Assets", "Art", "UI")

GAME_W, GAME_H = 1080, 1920

# Regions in SOURCE (1024x1536) coordinates that contain dynamic content and must be
# painted out of the background plate.
# Bounds were measured off the render by scanning for the gold border colour rather
# than eyeballed — see the gold() probe in the commit history.
DYNAMIC = [
    (326, 186, 710, 424),    # dealer's two cards (+ drop shadows)
    (272, 636, 686, 1020),   # player's two cards (+ drop shadows)
    (642, 796, 858, 972),    # bet chip stack
    # The four action buttons, individually — inpainting the whole strip flattened the
    # rail's texture and wiped the gold arc running behind it.
    (42, 1094, 252, 1264),
    (274, 1094, 493, 1264),
    (515, 1094, 733, 1264),
    (759, 1094, 972, 1264),
    # Top bar. The render bakes in a "5,000" balance and three icon buttons; those have
    # to be live, so they're painted out and rebuilt from sprites.
    (20, 20, 440, 120),
    (676, 22, 980, 120),
]

# The STAND button, used as the template for the live action buttons.
BUTTON_TEMPLATE = (274, 1094, 493, 1264)
BUTTON_INTERIOR = (300, 1116, 468, 1242)

def _boundary_mean(region, hole_box, band=14):
    """Average colour of a ring just outside the hole."""
    x0, y0, x1, y1 = hole_box
    px = region.load()
    tot = [0, 0, 0]
    n = 0
    for y in range(max(0, y0 - band), min(region.height, y1 + band)):
        for x in range(max(0, x0 - band), min(region.width, x1 + band)):
            if x0 <= x < x1 and y0 <= y < y1:
                continue  # inside the hole — not a valid sample
            c = px[x, y]
            tot[0] += c[0]; tot[1] += c[1]; tot[2] += c[2]
            n += 1
    return tuple(v // max(1, n) for v in tot)


def inpaint_diffuse(img, boxes, iterations=60, radius=20, pad=44):
    """
    Boundary-pinned diffusion inpaint.

    The hole is first flooded with the mean colour of the ring around it, then blurred
    repeatedly while the pixels *outside* it are re-imposed from the original on every
    pass, so colour flows inwards and converges on a smooth solution matching the
    boundary exactly.

    The seed matters: diffusing from the untouched region just smears the card's own
    bright pixels, and with a hole this wide the interior never washes out. Flooding
    first removes that content so diffusion only has to shape the gradient.
    """
    for (x0, y0, x1, y1) in boxes:
        rx0, ry0 = max(0, x0 - pad), max(0, y0 - pad)
        rx1, ry1 = min(img.width, x1 + pad), min(img.height, y1 + pad)

        region = img.crop((rx0, ry0, rx1, ry1))
        local = (x0 - rx0, y0 - ry0, x1 - rx0, y1 - ry0)

        hole = Image.new("L", region.size, 0)
        ImageDraw.Draw(hole).rectangle(
            [local[0], local[1], local[2] - 1, local[3] - 1], fill=255)
        hole = hole.filter(ImageFilter.GaussianBlur(3))

        seed = region.copy()
        ImageDraw.Draw(seed).rectangle(
            [local[0] - 2, local[1] - 2, local[2] + 1, local[3] + 1],
            fill=_boundary_mean(region, local))

        current = seed
        for _ in range(iterations):
            current = Image.composite(
                current.filter(ImageFilter.GaussianBlur(radius)), region, hole)

        img.paste(current, (rx0, ry0))
    return img


def add_grain(img, boxes, sigma=2.6, pad=12):
    """
    Diffusion produces a perfectly smooth fill, which reads as a patch against the felt's
    visible weave. Re-adding matched grain is what makes the repair disappear.
    """
    import random
    random.seed(23)
    px = img.load()
    for (x0, y0, x1, y1) in boxes:
        for y in range(max(0, y0 - pad), min(img.height, y1 + pad)):
            for x in range(max(0, x0 - pad), min(img.width, x1 + pad)):
                n = int(random.gauss(0, sigma))
                r, g, b = px[x, y]
                px[x, y] = (min(255, max(0, r + n)),
                            min(255, max(0, g + n)),
                            min(255, max(0, b + n)))
    return img


def build_background(src):
    plate = inpaint_diffuse(src.copy(), DYNAMIC)
    plate = add_grain(plate, DYNAMIC)

    # Scale to the game's width, then extend vertically: the render is 2:3 but the game
    # is 9:16, so ~300 rows are missing. Replicate the top and bottom edges rather than
    # stretching, which would distort the rail.
    scaled = plate.resize((GAME_W, int(plate.height * GAME_W / plate.width)), Image.LANCZOS)
    pad_top = 110
    pad_bottom = GAME_H - scaled.height - pad_top

    out = Image.new("RGB", (GAME_W, GAME_H))
    out.paste(scaled, (0, pad_top))
    out.paste(scaled.crop((0, 0, GAME_W, 4)).resize((GAME_W, pad_top), Image.BILINEAR), (0, 0))
    if pad_bottom > 0:
        bottom = scaled.crop((0, scaled.height - 4, GAME_W, scaled.height))
        out.paste(bottom.resize((GAME_W, pad_bottom), Image.BILINEAR), (0, pad_top + scaled.height))

    # Darken the synthetic strips slightly so they read as vignette, not as a seam.
    d = ImageDraw.Draw(out, "RGBA")
    for i in range(pad_top):
        a = int(150 * (1 - i / pad_top))
        d.line([(0, i), (GAME_W, i)], fill=(0, 0, 0, a))
    for i in range(max(0, pad_bottom)):
        a = int(170 * (i / max(1, pad_bottom)))
        y = pad_top + scaled.height + i
        d.line([(0, y), (GAME_W, y)], fill=(0, 0, 0, a))

    return out


def cut(src, box, out_path, resize=None):
    piece = src.crop(box).convert("RGBA")
    if resize:
        piece = piece.resize(resize, Image.LANCZOS)
    piece.save(out_path)
    print(f"  {os.path.relpath(out_path, ROOT)}  {piece.size}")
    return piece


def main():
    src = Image.open(SRC).convert("RGB")
    os.makedirs(TABLE_DIR, exist_ok=True)
    os.makedirs(CARDS_DIR, exist_ok=True)
    os.makedirs(UI_DIR, exist_ok=True)

    print("background:")
    bg = build_background(src)
    bg.save(os.path.join(TABLE_DIR, "TableBackground.png"))
    print(f"  Assets/Art/Table/TableBackground.png  {bg.size}")

    print("sprites lifted from the render:")
    # Gold filigree card back, straightened and sized to match the drawn faces.
    cut(src, (521, 205, 692, 404), os.path.join(CARDS_DIR, "card_Back.png"), (360, 504))
    # The bet chip is a UI element, so it belongs with the rest of the kit.
    cut(src, (655, 806, 846, 956), os.path.join(UI_DIR, "chip_stack.png"))

    # The button frame itself: crop the STAND button, then diffuse away its icon and
    # label so only the gold frame and dark fill remain. Nine-sliced in Unity.
    frame = src.crop(BUTTON_TEMPLATE).convert("RGB")
    ox, oy = BUTTON_TEMPLATE[0], BUTTON_TEMPLATE[1]
    inner = (BUTTON_INTERIOR[0] - ox, BUTTON_INTERIOR[1] - oy,
             BUTTON_INTERIOR[2] - ox, BUTTON_INTERIOR[3] - oy)
    frame = inpaint_diffuse(frame, [inner], iterations=45, radius=12, pad=18)
    frame.convert("RGBA").save(os.path.join(UI_DIR, "btn_action.b46.png"))
    print(f"  Assets/Art/UI/btn_action.b46.png  {frame.size}")

    # Action button icons — cropped tight from each button face.
    for name, box in (
        ("icon_hit",    (98, 1112, 210, 1210)),
        ("icon_stand",  (330, 1112, 442, 1210)),
        ("icon_double", (565, 1112, 677, 1210)),
        ("icon_split",  (800, 1112, 912, 1210)),
    ):
        cut(src, box, os.path.join(UI_DIR, f"{name}.png"))

    # Back arrow, lifted from the store render's top bar.
    store = Image.open(os.path.join(ROOT, "docs", "screenshots", "store.png")).convert("RGB")
    cut(store, (26, 36, 106, 112), os.path.join(UI_DIR, "icon_back.png"))

    print("done")


if __name__ == "__main__":
    main()
