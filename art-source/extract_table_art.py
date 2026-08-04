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

import colorsys
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

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
BUTTON_TEMPLATE = (277, 1097, 490, 1261)
# Must cover the icon AND the label; a tighter box left "STAND" ghosting through.
BUTTON_INTERIOR = (292, 1107, 475, 1252)

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


def button_frame(src_frame, tint, inset=20, bottom_inset=6, radius=22):
    """
    Builds a button by keeping the render's gold border and painting a fresh interior.

    Recolouring the existing interior does not work: the interior first has to be
    inpainted to remove the STAND icon and label, and diffusion seeds that fill from the
    surrounding ring — which is the gold border — so the "dark fill" comes out gold.
    Replacing the interior wholesale sidesteps that and gives a predictable result in
    any colour.
    """
    w, h = src_frame.size
    out = src_frame.copy()

    # The bottom inset is much smaller than the sides: the render's label sits low in
    # the button and a symmetric inset leaves "STAND" showing through every variant.
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [inset, inset, w - 1 - inset, h - 1 - bottom_inset], radius=radius, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(2))

    top = tuple(min(255, int(c * 2.0) + 10) for c in tint)
    bottom = tuple(max(0, int(c * 0.72)) for c in tint)
    strip = Image.new("RGB", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        strip.putpixel((0, y), tuple(
            int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    grad = strip.resize((w, h), Image.BILINEAR).convert("RGBA")

    out.paste(grad, (0, 0), mask)

    # Soft sheen across the upper third, as on the render's own buttons.
    sheen = Image.new("L", (w, h), 0)
    ImageDraw.Draw(sheen).ellipse([-w * 0.2, -h * 0.55, w * 1.2, h * 0.5], fill=64)
    sheen = ImageChops.multiply(sheen.filter(ImageFilter.GaussianBlur(10)), mask)
    out.paste(Image.new("RGBA", (w, h), (255, 250, 226, 255)), (0, 0), sheen)

    return out


def key_out_background(img, sample_box, tolerance=52, feather=1.4):
    """
    Makes the felt behind a lifted element transparent.

    The background is sampled from a corner of the crop rather than assumed, because the
    store rows sit on slightly different greens. Distance in RGB is enough to separate
    felt from gold chips; a light blur on the resulting alpha stops the edge looking cut
    out with scissors.
    """
    img = img.convert("RGBA")
    px = img.load()
    sx0, sy0, sx1, sy1 = sample_box
    n = 0
    acc = [0, 0, 0]
    for y in range(sy0, sy1):
        for x in range(sx0, sx1):
            c = px[x, y]
            acc[0] += c[0]; acc[1] += c[1]; acc[2] += c[2]
            n += 1
    bg = tuple(v // max(1, n) for v in acc)

    alpha = Image.new("L", img.size, 255)
    ap = alpha.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, _ = px[x, y]
            d = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
            ap[x, y] = 0 if d < tolerance else min(255, (d - tolerance) * 6)

    alpha = alpha.filter(ImageFilter.GaussianBlur(feather))
    img.putalpha(alpha)
    return img


# Store render geometry, measured by scanning for the gold row borders.
STORE_ROWS = [(64, 447, 960, 652), (48, 662, 972, 922),
              (64, 934, 960, 1134), (64, 1151, 960, 1354)]
STORE_TOPBAR = [(0, 20, 470, 122), (660, 24, 990, 120)]
STORE_ROW_TEMPLATE = (64, 447, 960, 652)
STORE_ROW_CONTENT = (86, 462, 944, 640)      # chips + amount + price, painted out
STORE_PRICE_BUTTON = (754, 495, 936, 604)
STORE_PRICE_TEXT = (766, 505, 926, 596)
# chips_1 is cropped clear of the BEST VALUE ribbon, which is lifted separately.
STORE_CHIPS = [(92, 458, 348, 642), (176, 716, 404, 908),
               (86, 946, 342, 1126), (86, 1164, 342, 1346)]
STORE_RIBBON = (48, 660, 268, 852)


# Menu render geometry: the three action rows and the top bar, measured the same way.
MENU_DYNAMIC = [
    (165, 736, 862, 895), (165, 904, 862, 1060), (165, 1065, 862, 1222),
    (20, 20, 440, 120), (660, 24, 990, 120),
]


def extract_menu(menu, table_dir):
    """
    Menu background plate.

    Everything static stays painted: the Ace crest, the BLACKJACK wordmark, the tagline,
    the house rules and the chip/card props in the corners. Only the three buttons and
    the top bar are removed, because those have to be live.
    """
    plate = inpaint_diffuse(menu.copy(), MENU_DYNAMIC)
    plate = add_grain(plate, MENU_DYNAMIC)

    scaled = plate.resize((GAME_W, int(plate.height * GAME_W / plate.width)), Image.LANCZOS)
    out = Image.new("RGB", (GAME_W, GAME_H))
    pad_top = 110
    out.paste(scaled, (0, pad_top))
    out.paste(scaled.crop((0, 0, GAME_W, 4)).resize((GAME_W, pad_top), Image.BILINEAR), (0, 0))
    rest = GAME_H - scaled.height - pad_top
    if rest > 0:
        out.paste(scaled.crop((0, scaled.height - 4, GAME_W, scaled.height))
                  .resize((GAME_W, rest), Image.BILINEAR), (0, pad_top + scaled.height))
    out.save(os.path.join(table_dir, "MenuBackground.png"))
    print(f"  Assets/Art/Table/MenuBackground.png  {out.size}")


def extract_store(store, ui_dir, table_dir):
    """Background plate plus the row furniture for the chip store."""
    plate = inpaint_diffuse(store.copy(), STORE_ROWS + STORE_TOPBAR)
    plate = add_grain(plate, STORE_ROWS + STORE_TOPBAR)

    scaled = plate.resize((GAME_W, int(plate.height * GAME_W / plate.width)), Image.LANCZOS)
    out = Image.new("RGB", (GAME_W, GAME_H))
    pad_top = 110
    out.paste(scaled, (0, pad_top))
    out.paste(scaled.crop((0, 0, GAME_W, 4)).resize((GAME_W, pad_top), Image.BILINEAR), (0, 0))
    rest = GAME_H - scaled.height - pad_top
    if rest > 0:
        out.paste(scaled.crop((0, scaled.height - 4, GAME_W, scaled.height))
                  .resize((GAME_W, rest), Image.BILINEAR), (0, pad_top + scaled.height))
    out.save(os.path.join(table_dir, "StoreBackground.png"))
    print(f"  Assets/Art/Table/StoreBackground.png  {out.size}")

    # Row frame: keep the render's gold border, repaint the interior. Inpainting the
    # interior instead seeds the fill from the gold border and comes out olive — the same
    # trap the action buttons hit.
    row = store.crop(STORE_ROW_TEMPLATE).convert("RGBA")
    button_frame(row, (13, 38, 20), inset=17, bottom_inset=17, radius=28).save(
        os.path.join(ui_dir, "store_row.b40.png"))
    print("  Assets/Art/UI/store_row.b40.png")

    # Price pill, with its baked "$1.99" removed.
    px0, py0 = STORE_PRICE_BUTTON[0], STORE_PRICE_BUTTON[1]
    price = store.crop(STORE_PRICE_BUTTON).convert("RGB")
    price = inpaint_diffuse(price, [(STORE_PRICE_TEXT[0] - px0, STORE_PRICE_TEXT[1] - py0,
                                     STORE_PRICE_TEXT[2] - px0, STORE_PRICE_TEXT[3] - py0)],
                            iterations=40, radius=12, pad=14)
    price.convert("RGBA").save(os.path.join(ui_dir, "btn_price.b34.png"))
    print("  Assets/Art/UI/btn_price.b34.png")

    # One chip stack per pack, keyed off the felt so they sit on any row.
    for i, box in enumerate(STORE_CHIPS):
        chip = store.crop(box)
        # Sample the felt from the bottom-right corner: the top-right of row 2 is ribbon.
        keyed = key_out_background(
            chip, (chip.width - 26, chip.height - 26, chip.width - 2, chip.height - 2))
        keyed.save(os.path.join(ui_dir, f"chips_{i}.png"))
    print(f"  Assets/Art/UI/chips_0..{len(STORE_CHIPS) - 1}.png")

    ribbon = store.crop(STORE_RIBBON)
    key_out_background(ribbon, (ribbon.width - 26, ribbon.height - 26,
                                ribbon.width - 2, ribbon.height - 2)).save(
        os.path.join(ui_dir, "badge_best_value.png"))
    print("  Assets/Art/UI/badge_best_value.png")


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
    # Action button icons — cropped tight from each button face.
    for name, box in (
        ("icon_hit",    (98, 1112, 210, 1210)),
        ("icon_stand",  (330, 1112, 442, 1210)),
        ("icon_double", (565, 1112, 677, 1210)),
        ("icon_split",  (800, 1112, 912, 1210)),
    ):
        cut(src, box, os.path.join(UI_DIR, f"{name}.png"))

    # Menu button frames, recoloured from the render's own action-button frame so the
    # menu and the table share one treatment. Only the dark interior is shifted; gold
    # pixels are detected and left alone, which is what keeps the metal looking like metal.
    raw = src.crop(BUTTON_TEMPLATE).convert("RGBA")
    for name, tint in (("btn_action", (13, 33, 18)),
                       ("btn_green", (14, 40, 19)),
                       ("btn_blue", (10, 24, 40)),
                       ("btn_red", (44, 12, 14)),
                       ("btn_dark", (10, 18, 15))):
        button_frame(raw, tint).save(os.path.join(UI_DIR, f"{name}.b46.png"))
        print(f"  Assets/Art/UI/{name}.b46.png")

    # Back arrow, lifted from the store render's top bar.
    store = Image.open(os.path.join(ROOT, "docs", "screenshots", "store.png")).convert("RGB")
    cut(store, (26, 36, 106, 112), os.path.join(UI_DIR, "icon_back.png"))

    print("store:")
    extract_store(store, UI_DIR, TABLE_DIR)

    print("menu:")
    menu = Image.open(os.path.join(ROOT, "docs", "screenshots", "main-menu.png")).convert("RGB")
    extract_menu(menu, TABLE_DIR)

    print("done")


if __name__ == "__main__":
    main()
