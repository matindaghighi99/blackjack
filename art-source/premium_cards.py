"""
Premium deck: 52 faces and a back, drawn from vector geometry.

Design rules
------------
* Bright warm-white stock with a whisper of paper grain; a hairline edge so a card
  reads cleanly against dark felt. No vignette, no heavy frame — the ink does the work.
* Indices are large and heavy enough to read at the size a phone shows a card in hand.
* Pips follow the traditional layouts; the lower half is inverted, as on a real deck.
* Courts share one treatment: a gold-ruled panel, a crown proper to the rank and a
  large serif letter, mirrored about the centre line like a double-ended court card.
"""

import math
import random

from premium_common import (
    SS, Image, ImageDraw, ImageFilter, np, font, ramp, to_image, seamless_noise,
    rounded_rect_mask, draw_suit, transform, path, mirror_x,
    INK_RED, INK_BLACK, STOCK_TOP, STOCK_BOTTOM, STOCK_EDGE, GOLD, GOLD_DEEP, GOLD_LIGHT,
    SERIF, SERIF_BOLD_SYSTEM, SANS_SEMIBOLD,
)

CARD_W, CARD_H = 360, 504
RADIUS = 18

SUITS = ["Clubs", "Diamonds", "Hearts", "Spades"]  # must match the C# Suit enum order
RED_SUITS = {"Hearts", "Diamonds"}
RANK_LABEL = {1: "A", 11: "J", 12: "Q", 13: "K"}

# Pip-area layout, in card pixels.
PIP_X0, PIP_X1 = 96, 264
PIP_Y0, PIP_Y1 = 92, 412
PIP_SIZE = 62

LAYOUTS = {
    2: [(0.5, 0.0), (0.5, 1.0)],
    3: [(0.5, 0.0), (0.5, 0.5), (0.5, 1.0)],
    4: [(0, 0), (1, 0), (0, 1), (1, 1)],
    5: [(0, 0), (1, 0), (0.5, 0.5), (0, 1), (1, 1)],
    6: [(0, 0), (1, 0), (0, 0.5), (1, 0.5), (0, 1), (1, 1)],
    7: [(0, 0), (1, 0), (0.5, 0.25), (0, 0.5), (1, 0.5), (0, 1), (1, 1)],
    8: [(0, 0), (1, 0), (0.5, 0.25), (0, 0.5), (1, 0.5), (0.5, 0.75), (0, 1), (1, 1)],
    9: [(0, 0), (1, 0), (0, 1 / 3), (1, 1 / 3), (0.5, 0.5), (0, 2 / 3), (1, 2 / 3), (0, 1), (1, 1)],
    10: [(0, 0), (1, 0), (0.5, 1 / 6), (0, 1 / 3), (1, 1 / 3), (0, 2 / 3), (1, 2 / 3),
         (0.5, 5 / 6), (0, 1), (1, 1)],
}


def ink(suit):
    return INK_RED if suit in RED_SUITS else INK_BLACK


# ---------------------------------------------------------------------------
# Stock
# ---------------------------------------------------------------------------

def stock(seed):
    """Card blank: warm white with a faint vertical fall-off and paper grain."""
    h, w = CARD_H, CARD_W
    t = np.linspace(0.0, 1.0, h)[:, None] * np.ones((1, w))
    base = ramp(t, [(0.0, STOCK_TOP), (1.0, STOCK_BOTTOM)])
    grain = seamless_noise(512, 0.8, seed)[:h, :w] * 1.5 + seamless_noise(512, 5.0, seed + 1)[:h, :w] * 0.45
    base[..., :3] += grain[..., None]
    img = to_image(base, seed=seed)

    mask = rounded_rect_mask(w, h, RADIUS)
    img.putalpha(mask)

    # Hairline edge, inset half a pixel, so the silhouette is crisp on dark felt.
    edge = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle(
        (SS, SS, w * SS - 1 - SS, h * SS - 1 - SS), radius=(RADIUS - 1) * SS,
        outline=STOCK_EDGE + (255,), width=int(1.4 * SS))
    edge = edge.resize((w, h), Image.LANCZOS)
    img.alpha_composite(edge)
    return img


# ---------------------------------------------------------------------------
# Indices
# ---------------------------------------------------------------------------

def draw_index(layer, label, suit, style="sans"):
    """Top-left index on a supersampled layer; the caller mirrors it to bottom-right."""
    d = ImageDraw.Draw(layer)
    colour = ink(suit) + (255,)
    cx = 38 * SS
    if style == "serif":
        f = font(SERIF_BOLD_SYSTEM, (60 if label != "10" else 54) * SS)
        tracking = -5 * SS if label == "10" else 0
    else:
        f = font(SANS_SEMIBOLD, (58 if label != "10" else 50) * SS)
        tracking = -3 * SS if label == "10" else 0

    widths = [f.getlength(ch) for ch in label]
    total = sum(widths) + tracking * (len(label) - 1)
    x = cx - total / 2
    top = 14 * SS
    for ch, cw in zip(label, widths):
        bbox = f.getbbox(ch)
        d.text((x, top - bbox[1] + 2 * SS), ch, font=f, fill=colour)
        x += cw + tracking
    cap = f.getbbox("A")
    pip_y = top + (cap[3] - cap[1]) + 32 * SS
    draw_suit(d, suit, cx, pip_y, 36 * SS, colour)


# ---------------------------------------------------------------------------
# Pips / aces
# ---------------------------------------------------------------------------

def draw_pips(layer, suit, rank):
    d = ImageDraw.Draw(layer)
    colour = ink(suit) + (255,)
    for (fx, fy) in LAYOUTS[rank]:
        x = (PIP_X0 + (PIP_X1 - PIP_X0) * fx) * SS
        y = (PIP_Y0 + (PIP_Y1 - PIP_Y0) * fy) * SS
        rot = math.pi if fy > 0.5 else 0.0
        draw_suit(d, suit, x, y, PIP_SIZE * SS, colour, rot)


def draw_ace(layer, suit):
    d = ImageDraw.Draw(layer)
    cx, cy = CARD_W / 2 * SS, CARD_H / 2 * SS
    colour = ink(suit) + (255,)
    if suit == "Spades":
        # The traditional showpiece: an oversized spade inside a fine gold ring.
        r = 118 * SS
        d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=GOLD_DEEP + (255,), width=int(2.2 * SS))
        r2 = r - 9 * SS
        d.ellipse((cx - r2, cy - r2, cx + r2, cy + r2), outline=GOLD + (200,), width=int(1.0 * SS))
        draw_suit(d, suit, cx, cy + 4 * SS, 150 * SS, colour)
    else:
        draw_suit(d, suit, cx, cy, 120 * SS, colour)


# ---------------------------------------------------------------------------
# Courts
# ---------------------------------------------------------------------------

PANEL = (70, 70, 290, 434)  # x0, y0, x1, y1


def crown(d, rank, cx, base_y, width, colour):
    """A small crown standing on base_y. K: five points, Q: arched with orbs, J: coronet."""
    s = width / 2.0
    band_h = s * 0.22
    d.rounded_rectangle((cx - s, base_y - band_h, cx + s, base_y), radius=band_h * 0.3, fill=colour)
    if rank == 13:
        pts = [(cx - s, base_y - band_h)]
        n = 5
        for i in range(n):
            x_peak = cx - s + (2 * s) * (i / (n - 1))
            h = s * (0.95 if i in (0, n - 1) else (1.25 if i == n // 2 else 1.05))
            pts.append((x_peak, base_y - band_h - h))
            if i < n - 1:
                x_val = cx - s + (2 * s) * ((i + 0.5) / (n - 1))
                pts.append((x_val, base_y - band_h - s * 0.42))
        pts.append((cx + s, base_y - band_h))
        d.polygon(pts, fill=colour)
        r = s * 0.1
        for i in range(n):
            x_peak = cx - s + (2 * s) * (i / (n - 1))
            h = s * (0.95 if i in (0, n - 1) else (1.25 if i == n // 2 else 1.05))
            d.ellipse((x_peak - r, base_y - band_h - h - r * 1.6, x_peak + r, base_y - band_h - h + r * 0.4), fill=colour)
    elif rank == 12:
        top = base_y - band_h
        for i, xo in enumerate((-0.62, 0.0, 0.62)):
            ax = cx + xo * s
            aw = s * 0.44
            ah = s * (0.95 if i == 1 else 0.72)
            d.pieslice((ax - aw, top - ah, ax + aw, top + ah), 180, 360, fill=colour)
            r = s * 0.12
            d.ellipse((ax - r, top - ah - r * 2.1, ax + r, top - ah - r * 0.1), fill=colour)
    else:
        top = base_y - band_h
        for xo in (-0.6, 0.0, 0.6):
            ax = cx + xo * s
            h = s * (0.62 if xo == 0 else 0.46)
            w = s * 0.2
            d.polygon([(ax, top - h), (ax + w, top - h * 0.45), (ax, top), (ax - w, top - h * 0.45)], fill=colour)


def draw_court(layer, suit, rank, seed):
    d = ImageDraw.Draw(layer)
    colour = ink(suit) + (255,)
    x0, y0, x1, y1 = [v * SS for v in PANEL]
    cx = (x0 + x1) / 2
    cy = (y0 + y1) / 2

    # Panel ground: a faint warm wash with a fine lozenge lattice, ruled in gold.
    d.rounded_rectangle((x0, y0, x1, y1), radius=8 * SS, fill=(246, 240, 226, 255))
    lattice = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lattice)
    step = 16 * SS
    for k in range(-40, 60):
        ox = x0 + k * step
        ld.line((ox, y0, ox + (y1 - y0), y1), fill=GOLD + (46,), width=SS)
        ld.line((ox, y1, ox + (y1 - y0), y0), fill=GOLD + (46,), width=SS)
    clip = Image.new("L", layer.size, 0)
    ImageDraw.Draw(clip).rounded_rectangle((x0, y0, x1, y1), radius=8 * SS, fill=255)
    lattice.putalpha(Image.composite(lattice.split()[3], Image.new("L", layer.size, 0), clip))
    layer.alpha_composite(lattice)
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((x0, y0, x1, y1), radius=8 * SS, outline=GOLD_DEEP + (255,), width=int(2.4 * SS))
    inset = 7 * SS
    d.rounded_rectangle((x0 + inset, y0 + inset, x1 - inset, y1 - inset), radius=4 * SS,
                        outline=GOLD + (190,), width=int(1.0 * SS))

    # One half of the double-ended figure; the other half is the same art turned 180.
    half = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    hd = ImageDraw.Draw(half)
    letter = RANK_LABEL[rank]
    f = font(SERIF_BOLD_SYSTEM, 128 * SS)
    bbox = f.getbbox(letter)
    lw, lh = bbox[2] - bbox[0], bbox[3] - bbox[1]
    letter_cy = y0 + (cy - y0) * 0.56
    hd.text((cx - lw / 2 - bbox[0], letter_cy - lh / 2 - bbox[1]), letter, font=f, fill=colour)
    crown(hd, rank, cx, letter_cy - lh / 2 - 12 * SS, 58 * SS, GOLD_DEEP + (255,))
    # Keep only the top half so the mirrored copy meets it cleanly at the centre rule.
    cut = Image.new("L", layer.size, 0)
    ImageDraw.Draw(cut).rectangle((0, 0, layer.size[0], cy), fill=255)
    half.putalpha(Image.composite(half.split()[3], Image.new("L", layer.size, 0), cut))
    layer.alpha_composite(half)
    layer.alpha_composite(half.rotate(180, resample=Image.BICUBIC, center=(cx, cy)))

    # Centre rule with a medallion carrying the suit.
    d = ImageDraw.Draw(layer)
    d.line((x0 + 16 * SS, cy, x1 - 16 * SS, cy), fill=GOLD_DEEP + (220,), width=int(1.2 * SS))
    r = 21 * SS
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(250, 246, 236, 255), outline=GOLD_DEEP + (255,), width=int(1.6 * SS))
    draw_suit(d, suit, cx, cy, 24 * SS, colour)


# ---------------------------------------------------------------------------
# Assembly
# ---------------------------------------------------------------------------

def make_face(suit, rank, style="sans"):
    seed = SUITS.index(suit) * 13 + rank
    card = stock(seed)
    layer = Image.new("RGBA", (CARD_W * SS, CARD_H * SS), (0, 0, 0, 0))

    label = RANK_LABEL.get(rank, str(rank))
    corner = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    draw_index(corner, label, suit, style)
    layer.alpha_composite(corner)
    layer.alpha_composite(corner.rotate(180, resample=Image.BICUBIC))

    if rank == 1:
        draw_ace(layer, suit)
    elif rank >= 11:
        draw_court(layer, suit, rank, seed)
    else:
        draw_pips(layer, suit, rank)

    ink_layer = layer.resize((CARD_W, CARD_H), Image.LANCZOS)
    card.alpha_composite(ink_layer)
    # Re-clip so nothing pokes past the rounded corners.
    card.putalpha(Image.composite(card.split()[3], Image.new("L", card.size, 0), rounded_rect_mask(CARD_W, CARD_H, RADIUS)))
    return card


BACK_FIELD = (18, 20, 22)


def make_back():
    """Deep onyx field inside an ivory border, a fine gold lattice and a centre medallion."""
    card = stock(99)
    w, h = CARD_W * SS, CARD_H * SS
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    b = 16 * SS
    field = (b, b, w - b, h - b)
    d.rounded_rectangle(field, radius=10 * SS, fill=BACK_FIELD + (255,))

    # Guilloche-style lattice: two families of fine diagonal lines.
    lat = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lat)
    step = 18 * SS
    for k in range(-60, 80):
        ox = k * step
        ld.line((ox, 0, ox + h, h), fill=GOLD + (70,), width=int(1.1 * SS))
        ld.line((ox, h, ox + h, 0), fill=GOLD + (70,), width=int(1.1 * SS))
    # Small dots on the lattice crossings add texture without adding noise. Lines of
    # the first family satisfy x - y = a*step, the second x + y = b*step + h.
    for a in range(-60, 80):
        for b in range(-60, 80):
            x = ((a + b) * step + h) / 2
            y = ((b - a) * step + h) / 2
            if 0 <= x <= w and 0 <= y <= h:
                r = 1.4 * SS
                ld.ellipse((x - r, y - r, x + r, y + r), fill=GOLD_LIGHT + (70,))
    clip = Image.new("L", (w, h), 0)
    inner = 8 * SS
    ImageDraw.Draw(clip).rounded_rectangle((field[0] + inner, field[1] + inner, field[2] - inner, field[3] - inner),
                                           radius=6 * SS, fill=255)
    lat.putalpha(Image.composite(lat.split()[3], Image.new("L", (w, h), 0), clip))
    layer.alpha_composite(lat)

    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((field[0] + inner, field[1] + inner, field[2] - inner, field[3] - inner),
                        radius=6 * SS, outline=GOLD + (200,), width=int(1.2 * SS))

    # Medallion.
    cx, cy = w / 2, h / 2
    r = 70 * SS
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=BACK_FIELD + (255,), outline=GOLD + (255,), width=int(2.0 * SS))
    r2 = r - 8 * SS
    d.ellipse((cx - r2, cy - r2, cx + r2, cy + r2), outline=GOLD_DEEP + (255,), width=int(1.0 * SS))
    draw_suit(d, "Spades", cx, cy + 3 * SS, 70 * SS, GOLD + (255,))

    card.alpha_composite(layer.resize((CARD_W, CARD_H), Image.LANCZOS))
    card.putalpha(Image.composite(card.split()[3], Image.new("L", card.size, 0), rounded_rect_mask(CARD_W, CARD_H, RADIUS)))
    return card
