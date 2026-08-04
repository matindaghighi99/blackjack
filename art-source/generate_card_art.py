#!/usr/bin/env python3
"""
Procedurally generates the playing-card sprites and table felt for the game.

Everything is drawn as vector shapes and supersampled, so the art is reproducible
from source (no binary blobs of unknown provenance, no licensing questions) and can
be regenerated at any resolution by changing CARD_W / CARD_H below.

    python3 art-source/generate_card_art.py

Output:
    Assets/Art/Cards/card_<Suit>_<01..13>.png   52 faces, Suit matching the C# enum
    (card_Back.png comes from extract_table_art.py, not this script)
    Assets/Art/Table/Felt.png                   table background
"""

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
CARDS_DIR = os.path.join(ROOT, "Assets", "Art", "Cards")
TABLE_DIR = os.path.join(ROOT, "Assets", "Art", "Table")

CARD_W, CARD_H = 360, 504
CORNER_R = 28
SS = 4  # supersampling factor

FELT_W, FELT_H = 1080, 1920

FONT_BOLD = "/usr/local/lib/python3.10/dist-packages/matplotlib/mpl-data/fonts/ttf/DejaVuSans-Bold.ttf"

# Suit order must match BlackjackGame.Blackjack.Cards.Suit
SUITS = ["Clubs", "Diamonds", "Hearts", "Spades"]
# Ink deepened to sit on warm stock; pure red/black looked garish against ivory.
RED = (176, 32, 38)
BLACK = (28, 28, 32)
SUIT_COLOR = {"Clubs": BLACK, "Spades": BLACK, "Hearts": RED, "Diamonds": RED}

RANK_LABEL = {1: "A", 11: "J", 12: "Q", 13: "K"}

# Stock sampled from the rendered cards in docs/screenshots/game-table.png (#F5E6BF).
# It is a warm ivory, not white — that single change does most of the work in making
# the drawn cards sit alongside the painted ones in the render.
FACE_TOP = (250, 241, 212)
FACE_BOT = (238, 221, 178)
FACE_EDGE = (191, 168, 122)
GOLD_LINE = (176, 141, 74)

BACK_BG = (24, 54, 96)
BACK_ACCENT = (72, 116, 178)
BACK_EDGE = (245, 245, 245)


# ---------------------------------------------------------------------------
# Suit pip drawing (normalised: s is the pip's nominal half-size)
# ---------------------------------------------------------------------------

def _heart(d, cx, cy, s, color, flip=False):
    f = -1 if flip else 1
    r = s * 0.30
    d.ellipse([cx - s * 0.25 - r, cy - f * s * 0.20 - r,
               cx - s * 0.25 + r, cy - f * s * 0.20 + r], fill=color)
    d.ellipse([cx + s * 0.25 - r, cy - f * s * 0.20 - r,
               cx + s * 0.25 + r, cy - f * s * 0.20 + r], fill=color)
    d.polygon([(cx - s * 0.54, cy - f * s * 0.14),
               (cx + s * 0.54, cy - f * s * 0.14),
               (cx, cy + f * s * 0.58)], fill=color)


def _stem(d, cx, cy, s, color):
    d.polygon([(cx - s * 0.07, cy + s * 0.16),
               (cx + s * 0.07, cy + s * 0.16),
               (cx + s * 0.24, cy + s * 0.62),
               (cx - s * 0.24, cy + s * 0.62)], fill=color)


def draw_pip(d, suit, cx, cy, s, color, inverted=False):
    """Draws one suit pip centred at (cx, cy). `inverted` rotates it 180 degrees."""
    if inverted:
        cy_ = cy
        if suit == "Hearts":
            _heart(d, cx, cy_, s, color, flip=True)
            return
        if suit == "Diamonds":
            draw_pip(d, "Diamonds", cx, cy_, s, color)
            return
        if suit == "Spades":
            _heart(d, cx, cy_, s, color, flip=False)
            _stem_inv(d, cx, cy_, s, color)
            return
        if suit == "Clubs":
            _clubs(d, cx, cy_, s, color, flip=True)
            return

    if suit == "Hearts":
        _heart(d, cx, cy, s, color, flip=False)
    elif suit == "Diamonds":
        d.polygon([(cx, cy - s * 0.62), (cx + s * 0.44, cy),
                   (cx, cy + s * 0.62), (cx - s * 0.44, cy)], fill=color)
    elif suit == "Spades":
        _heart(d, cx, cy, s, color, flip=True)  # point-up heart == spade body
        _stem(d, cx, cy, s, color)
    elif suit == "Clubs":
        _clubs(d, cx, cy, s, color, flip=False)


def _stem_inv(d, cx, cy, s, color):
    d.polygon([(cx - s * 0.07, cy - s * 0.16),
               (cx + s * 0.07, cy - s * 0.16),
               (cx + s * 0.24, cy - s * 0.62),
               (cx - s * 0.24, cy - s * 0.62)], fill=color)


def _clubs(d, cx, cy, s, color, flip=False):
    f = -1 if flip else 1
    r = s * 0.29
    for dx, dy in ((0.0, -0.30), (-0.33, 0.12), (0.33, 0.12)):
        ccx, ccy = cx + s * dx, cy + f * s * dy
        d.ellipse([ccx - r, ccy - r, ccx + r, ccy + r], fill=color)
    if flip:
        _stem_inv(d, cx, cy, s, color)
    else:
        _stem(d, cx, cy, s, color)


# ---------------------------------------------------------------------------
# Pip layouts (x, y) in card fractions. y > 0.5 pips are drawn inverted.
# ---------------------------------------------------------------------------

L, C, R = 0.285, 0.5, 0.715
TOP, UPR, MID, LWR, BOT = 0.175, 0.385, 0.5, 0.615, 0.825

LAYOUTS = {
    2:  [(C, TOP), (C, BOT)],
    3:  [(C, TOP), (C, MID), (C, BOT)],
    4:  [(L, TOP), (R, TOP), (L, BOT), (R, BOT)],
    5:  [(L, TOP), (R, TOP), (C, MID), (L, BOT), (R, BOT)],
    6:  [(L, TOP), (R, TOP), (L, MID), (R, MID), (L, BOT), (R, BOT)],
    7:  [(L, TOP), (R, TOP), (C, 0.3375), (L, MID), (R, MID), (L, BOT), (R, BOT)],
    8:  [(L, TOP), (R, TOP), (C, 0.3375), (L, MID), (R, MID),
         (C, 0.6625), (L, BOT), (R, BOT)],
    9:  [(L, TOP), (R, TOP), (L, UPR), (R, UPR), (C, MID),
         (L, LWR), (R, LWR), (L, BOT), (R, BOT)],
    10: [(L, TOP), (R, TOP), (C, 0.28), (L, UPR), (R, UPR),
         (L, LWR), (R, LWR), (C, 0.72), (L, BOT), (R, BOT)],
}


def load_font(size):
    return ImageFont.truetype(FONT_BOLD, size)


def draw_rounded(d, box, radius, fill, outline=None, width=1):
    d.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def card_plate(w, h):
    """
    The blank card: ivory stock with a vertical gradient, a warm edge and a soft inner
    vignette so the face is not dead flat.
    """
    plate = Image.new("RGBA", (w, h), (0, 0, 0, 0))

    grad = Image.new("RGB", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        grad.putpixel((0, y), tuple(
            int(FACE_TOP[i] + (FACE_BOT[i] - FACE_TOP[i]) * t) for i in range(3)))
    grad = grad.resize((w, h), Image.BILINEAR).convert("RGBA")

    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, w - 1, h - 1], radius=CORNER_R * SS, fill=255)
    plate.paste(grad, (0, 0), mask)

    # Edge vignette: darken a band just inside the border.
    vig = Image.new("L", (w, h), 0)
    ImageDraw.Draw(vig).rounded_rectangle(
        [0, 0, w - 1, h - 1], radius=CORNER_R * SS, fill=255)
    inner = Image.new("L", (w, h), 0)
    ImageDraw.Draw(inner).rounded_rectangle(
        [10 * SS, 10 * SS, w - 1 - 10 * SS, h - 1 - 10 * SS],
        radius=(CORNER_R - 6) * SS, fill=255)
    ring = Image.composite(vig, Image.new("L", (w, h), 0),
                           inner.point(lambda v: 255 - v))
    ring = ring.filter(ImageFilter.GaussianBlur(6 * SS))
    plate.paste(Image.new("RGBA", (w, h), (120, 96, 52, 60)), (0, 0), ring)

    d = ImageDraw.Draw(plate)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=CORNER_R * SS,
                        outline=FACE_EDGE, width=max(1, 2 * SS))
    return plate


def make_card(suit, rank):
    w, h = CARD_W * SS, CARD_H * SS
    img = card_plate(w, h)
    d = ImageDraw.Draw(img)
    color = SUIT_COLOR[suit]

    label = RANK_LABEL.get(rank, str(rank))

    # --- corner indices -----------------------------------------------------
    idx_font = load_font(int(58 * SS))
    corner = Image.new("RGBA", (int(80 * SS), int(132 * SS)), (0, 0, 0, 0))
    cd = ImageDraw.Draw(corner)
    bbox = cd.textbbox((0, 0), label, font=idx_font)
    cd.text(((corner.width - (bbox[2] - bbox[0])) / 2 - bbox[0], -bbox[1]),
            label, font=idx_font, fill=color)
    draw_pip(cd, suit, corner.width / 2, 104 * SS, 22 * SS, color)

    img.alpha_composite(corner, (int(16 * SS), int(16 * SS)))
    img.alpha_composite(corner.rotate(180, expand=True),
                        (w - corner.width - int(16 * SS), h - corner.height - int(16 * SS)))

    # --- centre --------------------------------------------------------------
    if rank in LAYOUTS:
        for fx, fy in LAYOUTS[rank]:
            draw_pip(d, suit, fx * w, fy * h, 34 * SS, color, inverted=fy > 0.52)
    elif rank == 1:
        draw_pip(d, suit, w / 2, h / 2, 92 * SS, color)
    else:
        # Court cards: a clean monogram panel rather than fake royal artwork.
        pad_x, pad_y = int(w * 0.19), int(h * 0.165)
        # Double gold rule around the monogram, echoing the render's court borders.
        draw_rounded(d, [pad_x, pad_y, w - pad_x, h - pad_y], 20 * SS,
                     fill=None, outline=GOLD_LINE, width=max(1, 3 * SS))
        draw_rounded(d, [pad_x + 6 * SS, pad_y + 6 * SS,
                         w - pad_x - 6 * SS, h - pad_y - 6 * SS], 15 * SS,
                     fill=None, outline=color, width=max(1, 2 * SS))
        court_font = load_font(int(150 * SS))
        bb = d.textbbox((0, 0), label, font=court_font)
        d.text((w / 2 - (bb[2] - bb[0]) / 2 - bb[0], h * 0.5 - (bb[3] - bb[1]) / 2 - bb[1] - 22 * SS),
               label, font=court_font, fill=color)
        draw_pip(d, suit, w / 2, h * 0.685, 40 * SS, color)

    return img.resize((CARD_W, CARD_H), Image.LANCZOS)


def make_back():
    w, h = CARD_W * SS, CARD_H * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    draw_rounded(d, [0, 0, w - 1, h - 1], CORNER_R * SS, fill=BACK_EDGE)
    inset = 9 * SS
    draw_rounded(d, [inset, inset, w - 1 - inset, h - 1 - inset],
                 (CORNER_R - 7) * SS, fill=BACK_BG)

    # Diagonal lattice, clipped to the inner panel.
    lattice = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lattice)
    step = 34 * SS
    for i in range(-h, w + h, step):
        ld.line([(i, 0), (i + h, h)], fill=BACK_ACCENT, width=max(1, 3 * SS))
        ld.line([(i, h), (i + h, 0)], fill=BACK_ACCENT, width=max(1, 3 * SS))

    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [inset * 2, inset * 2, w - 1 - inset * 2, h - 1 - inset * 2],
        radius=(CORNER_R - 9) * SS, fill=255)
    img.paste(lattice, (0, 0), mask)

    # Centre emblem
    cx, cy, s = w / 2, h / 2, 66 * SS
    d.ellipse([cx - s, cy - s, cx + s, cy + s], fill=BACK_BG, outline=BACK_EDGE,
              width=max(1, 4 * SS))
    d.polygon([(cx, cy - s * 0.5), (cx + s * 0.36, cy), (cx, cy + s * 0.5),
               (cx - s * 0.36, cy)], fill=BACK_EDGE)

    return img.resize((CARD_W, CARD_H), Image.LANCZOS)


def make_felt():
    w, h = FELT_W, FELT_H
    base = Image.new("RGB", (w, h), (11, 46, 33))
    d = ImageDraw.Draw(base)

    # Radial pool of light around the table centre.
    cx, cy = w / 2, h * 0.46
    max_r = math.hypot(w, h) * 0.62
    for i in range(140, 0, -1):
        t = i / 140.0
        r = max_r * t
        c = (int(11 + 34 * (1 - t)), int(46 + 62 * (1 - t)), int(33 + 44 * (1 - t)))
        d.ellipse([cx - r, cy - r * 0.85, cx + r, cy + r * 0.85], fill=c)

    # Felt tooth.
    random.seed(7)
    noise = Image.new("L", (w // 2, h // 2))
    noise.putdata([random.randint(112, 143) for _ in range((w // 2) * (h // 2))])
    noise = noise.resize((w, h), Image.BILINEAR).filter(ImageFilter.GaussianBlur(0.6))
    base = Image.blend(base, Image.merge("RGB", (noise, noise, noise)), 0.055)

    d = ImageDraw.Draw(base)

    # The dealer's arc, curving down towards the player ("cup" shaped, like the real
    # table edge). PIL angles run clockwise from 3 o'clock and y grows downward, so
    # 20 -> 160 sweeps through 6 o'clock and gives the lower half of the ellipse.
    gold = (196, 160, 82)
    d.arc([w * 0.02, -h * 0.10, w * 0.98, h * 0.34], start=20, end=160, fill=gold, width=5)
    d.arc([w * 0.07, -h * 0.07, w * 0.93, h * 0.30], start=24, end=156,
          fill=(150, 122, 62), width=2)

    # Rule text sits low, clear of the UI panels that overlay the middle of the screen.
    # Values match ClassicRules: 3:2 naturals, dealer hits soft 17.
    f1 = load_font(46)
    f2 = load_font(28)
    for text, font, y, col in (("BLACKJACK PAYS 3 TO 2", f1, h * 0.905, gold),
                               ("Dealer must hit soft 17", f2, h * 0.945, (150, 176, 162))):
        bb = d.textbbox((0, 0), text, font=font)
        d.text((w / 2 - (bb[2] - bb[0]) / 2 - bb[0], y), text, font=font, fill=col)

    # Vignette.
    vig = Image.new("L", (w, h), 0)
    ImageDraw.Draw(vig).ellipse([-w * 0.28, -h * 0.16, w * 1.28, h * 1.16], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(190))
    dark = Image.new("RGB", (w, h), (4, 18, 13))
    return Image.composite(base, dark, vig)


def main():
    os.makedirs(CARDS_DIR, exist_ok=True)
    os.makedirs(TABLE_DIR, exist_ok=True)

    count = 0
    for suit in SUITS:
        for rank in range(1, 14):
            make_card(suit, rank).save(
                os.path.join(CARDS_DIR, f"card_{suit}_{rank:02d}.png"))
            count += 1

    # NOTE: card_Back.png is deliberately NOT written here. The gold filigree back is
    # lifted from the concept render by extract_table_art.py, which looks far better than
    # the drawn one — and whichever script ran last used to win, silently.
    # make_back() is kept as the fallback design if that render ever goes away.
    make_felt().save(os.path.join(TABLE_DIR, "Felt.png"))

    print(f"Wrote {count} card faces + back to {CARDS_DIR}")
    print(f"Wrote table felt to {TABLE_DIR}")


if __name__ == "__main__":
    main()
