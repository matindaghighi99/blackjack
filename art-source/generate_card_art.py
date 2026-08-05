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

Design notes
------------
The deck is styled to sit beside the gold-and-ivory concept render:

* Indices use the same serif display face as the UI (TeX Gyre Bonum Bold), not a
  UI sans — a card index in DejaVu reads as a debug asset next to the render.
* Pips are drawn from parametric curves (a real cardioid heart, a bowed diamond)
  and shaded with a vertical gradient, an inner sheen and a soft contact shadow,
  so they have the slight dimensionality of printed ink rather than flat vector fill.
* Courts are a single consistent treatment across all twelve cards: a diapered
  tapestry panel in a double gold frame, a crown proper to the rank, and a large
  inlaid monogram — mirrored top/bottom like a real court card. One style for the
  whole deck beats three lush paintings sitting next to nine placeholders.
* The ace of spades gets the traditional oversized ornament.
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

# The game's own display serif, so card indices and UI titles share one voice.
FONT_SERIF = os.path.join(ROOT, "Assets", "Fonts", "TeXGyreBonum-Bold.otf")
FONT_FALLBACK = "/usr/local/lib/python3.10/dist-packages/matplotlib/mpl-data/fonts/ttf/DejaVuSans-Bold.ttf"

# Suit order must match BlackjackGame.Blackjack.Cards.Suit
SUITS = ["Clubs", "Diamonds", "Hearts", "Spades"]
# Ink deepened to sit on warm stock; pure red/black looked garish against ivory.
RED = (168, 28, 36)
BLACK = (30, 30, 36)
SUIT_COLOR = {"Clubs": BLACK, "Spades": BLACK, "Hearts": RED, "Diamonds": RED}

RANK_LABEL = {1: "A", 11: "J", 12: "Q", 13: "K"}

# Stock sampled from the rendered cards in docs/screenshots/game-table.png (#F5E6BF).
# It is a warm ivory, not white — that single change does most of the work in making
# the drawn cards sit alongside the painted ones in the render.
FACE_TOP = (250, 241, 212)
FACE_BOT = (238, 221, 178)
FACE_EDGE = (191, 168, 122)
GOLD_LINE = (176, 141, 74)
GOLD_DEEP = (135, 103, 48)
GOLD_HI = (232, 202, 132)

BACK_BG = (24, 54, 96)
BACK_ACCENT = (72, 116, 178)
BACK_EDGE = (245, 245, 245)


def load_font(size, serif=True):
    path = FONT_SERIF if serif and os.path.exists(FONT_SERIF) else FONT_FALLBACK
    return ImageFont.truetype(path, size)


def _shade(color, k):
    """Lightens (k>1) or darkens (k<1) an RGB colour."""
    if k >= 1:
        return tuple(min(255, int(c + (255 - c) * (k - 1))) for c in color)
    return tuple(int(c * k) for c in color)


# ---------------------------------------------------------------------------
# Parametric pip outlines (unit-ish coordinates, later scaled by s)
# ---------------------------------------------------------------------------

def _heart_points(n=120):
    """Classic cardioid heart, normalised to roughly a unit box."""
    pts = []
    for i in range(n):
        t = math.pi * 2 * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x / 17.0, -y / 17.0))
    return pts


_HEART = _heart_points()


def _diamond_points(bow=0.085, n=28):
    """A diamond whose sides bow gently outward — printed diamonds are convex."""
    corners = [(0, -0.62), (0.46, 0), (0, 0.62), (-0.46, 0)]
    pts = []
    for i in range(4):
        ax, ay = corners[i]
        bx, by = corners[(i + 1) % 4]
        mx, my = (ax + bx) / 2, (ay + by) / 2
        # Push the midpoint outward along its normal.
        nx, ny = mx, my
        ln = math.hypot(nx, ny) or 1.0
        cx_, cy_ = mx + nx / ln * bow, my + ny / ln * bow
        for j in range(n):
            t = j / n
            # Quadratic bezier a -> control -> b
            x = (1 - t) ** 2 * ax + 2 * (1 - t) * t * cx_ + t ** 2 * bx
            y = (1 - t) ** 2 * ay + 2 * (1 - t) * t * cy_ + t ** 2 * by
            pts.append((x, y))
    return pts


_DIAMOND = _diamond_points()


def _spade_stem(s):
    """Concave-flanked stem with a flared foot, as on printed spades/clubs."""
    pts = []
    steps = 16
    for i in range(steps + 1):          # right flank, top to bottom (concave)
        t = i / steps
        x = 0.055 + 0.185 * t * t
        y = 0.14 + 0.50 * t
        pts.append((x, y))
    for i in range(steps + 1):          # left flank, bottom to top
        t = 1 - i / steps
        x = -(0.055 + 0.185 * t * t)
        y = 0.14 + 0.50 * t
        pts.append((x, y))
    return [(x * s, y * s) for x, y in pts]


def _pip_polys(suit, s):
    """Returns a list of polygons (point lists, centred on origin) making up a pip."""
    polys = []
    if suit == "Hearts":
        polys.append([(x * s * 0.62, y * s * 0.62 + s * 0.02) for x, y in _HEART])
    elif suit == "Diamonds":
        polys.append([(x * s, y * s) for x, y in _DIAMOND])
    elif suit == "Spades":
        body = [(x * s * 0.60, -y * s * 0.60 - s * 0.06) for x, y in _HEART]
        polys.append(body)
        polys.append(_spade_stem(s))
    elif suit == "Clubs":
        r = s * 0.305
        for dx, dy in ((0.0, -0.315), (-0.335, 0.115), (0.335, 0.115)):
            cx_, cy_ = s * dx, s * dy
            circ = [(cx_ + r * math.cos(a), cy_ + r * math.sin(a))
                    for a in [math.tau * i / 40 for i in range(40)]]
            polys.append(circ)
        polys.append(_spade_stem(s))
    return polys


def pip_layer(suit, s, color, ss=1):
    """
    One shaded pip on its own transparent layer (sized to fit), centred.

    The pip is built as a mask, then filled with a vertical gradient, kissed with an
    inner sheen at the top and set on a soft contact shadow — ink with a little life
    in it, matching the moulded gold of the UI kit.
    """
    pad = int(s * 0.9)
    size = int(s * 2 + pad)
    cx = cy = size / 2

    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    for poly in _pip_polys(suit, s):
        md.polygon([(cx + x, cy + y) for x, y in poly], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(0.5 * ss))

    grad = Image.new("RGB", (1, size))
    top, bot = _shade(color, 1.22), _shade(color, 0.72)
    for y in range(size):
        t = y / max(1, size - 1)
        grad.putpixel((0, y), tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)))
    grad = grad.resize((size, size), Image.BILINEAR)

    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Contact shadow first, so the fill sits on top of it.
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow.paste(Image.new("RGBA", (size, size), (30, 18, 8, 92)), (0, 0), mask)
    shadow = shadow.filter(ImageFilter.GaussianBlur(1.6 * ss))
    layer.alpha_composite(shadow, (int(0.5 * ss), int(2.2 * ss)))

    body = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    body.paste(grad.convert("RGBA"), (0, 0), mask)

    # Inner sheen: a soft light pass across the pip's upper third.
    sheen_mask = mask.copy().filter(ImageFilter.GaussianBlur(2.5 * ss))
    sheen = Image.new("L", (size, size), 0)
    sd = ImageDraw.Draw(sheen)
    sd.ellipse([size * 0.14, -size * 0.25, size * 0.86, size * 0.42], fill=70)
    sheen = ImageChops_multiply(sheen, sheen_mask)
    body.alpha_composite(
        Image.merge("RGBA", [Image.new("L", (size, size), 255)] * 3 + [sheen]))

    layer.alpha_composite(body)
    return layer


def ImageChops_multiply(a, b):
    from PIL import ImageChops
    return ImageChops.multiply(a, b)


def draw_pip(img, suit, cx, cy, s, color, inverted=False):
    layer = pip_layer(suit, s, color, ss=SS)
    if inverted:
        layer = layer.rotate(180)
    img.alpha_composite(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)))


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


def serif_text_layer(label, px, color, tracking=0):
    """A text layer with a hint of letterpress: dark below-right, light above-left."""
    fnt = load_font(px)
    probe = ImageDraw.Draw(Image.new("RGBA", (8, 8)))
    bbox = probe.textbbox((0, 0), label, font=fnt)
    w = bbox[2] - bbox[0] + tracking * max(0, len(label) - 1) + 8
    h = bbox[3] - bbox[1] + 8
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    def put(dx, dy, fill):
        x = 4 - bbox[0] + dx
        for i, ch in enumerate(label):
            d.text((x, 4 - bbox[1] + dy), ch, font=fnt, fill=fill)
            x += d.textbbox((0, 0), ch, font=fnt)[2] + tracking

    put(1.2, 1.6, _shade(color, 0.55) + (170,))
    put(-0.8, -1.2, _shade(color, 1.55) + (150,))
    put(0, 0, color)
    return layer


def corner_index(suit, rank, color):
    """The rank-and-pip corner block, on its own layer."""
    label = RANK_LABEL.get(rank, str(rank))
    px = int((46 if label == "10" else 56) * SS)
    text = serif_text_layer(label, px, color, tracking=int(-3 * SS) if label == "10" else 0)

    pip = pip_layer(suit, 21 * SS, color, ss=SS)
    w = max(text.width, pip.width) + 4 * SS
    layer = Image.new("RGBA", (w, text.height + pip.height - int(6 * SS)), (0, 0, 0, 0))
    layer.alpha_composite(text, ((w - text.width) // 2, 0))
    layer.alpha_composite(pip, ((w - pip.width) // 2, text.height - int(6 * SS)))
    return layer


# ---------------------------------------------------------------------------
# Court cards
# ---------------------------------------------------------------------------

def _crown_layer(rank, wpx, color):
    """
    A small crown proper to the rank: five-point with jewels for the king, a pearled
    coronet for the queen, a simple three-point circlet for the jack.
    """
    w = wpx
    h = int(wpx * 0.62)
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    base_y = int(h * 0.78)
    band = int(h * 0.16)

    def jewel(cx, cy, r):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color,
                  outline=GOLD_DEEP, width=max(1, SS))

    if rank == 13:      # King — five points, cross-topped centre
        pts = 5
        for i in range(pts):
            x0 = w * i / pts
            x1 = w * (i + 1) / pts
            xm = (x0 + x1) / 2
            d.polygon([(x0, base_y), (xm, h * 0.10 if i == pts // 2 else h * 0.22),
                       (x1, base_y)], fill=GOLD_LINE, outline=GOLD_DEEP, width=max(1, SS))
            jewel(xm, h * (0.16 if i == pts // 2 else 0.27), h * 0.055)
    elif rank == 12:    # Queen — arcs with pearls
        pts = 4
        for i in range(pts):
            x0 = w * i / pts
            x1 = w * (i + 1) / pts
            d.pieslice([x0, h * 0.18, x1, base_y + h * 0.35], 180, 360,
                       fill=GOLD_LINE, outline=GOLD_DEEP, width=max(1, SS))
        for i in range(pts + 1):
            jewel(w * i / pts if 0 < i < pts else (h * 0.06 if i == 0 else w - h * 0.06),
                  h * 0.20, h * 0.06)
    else:               # Jack — plain three-point circlet
        pts = 3
        for i in range(pts):
            x0 = w * i / pts
            x1 = w * (i + 1) / pts
            xm = (x0 + x1) / 2
            d.polygon([(x0, base_y), (xm, h * 0.26), (x1, base_y)],
                      fill=GOLD_LINE, outline=GOLD_DEEP, width=max(1, SS))

    d.rectangle([0, base_y, w, base_y + band], fill=GOLD_LINE,
                outline=GOLD_DEEP, width=max(1, SS))
    hi = ImageDraw.Draw(layer)
    hi.rectangle([0, base_y + max(1, SS), w, base_y + max(2, 2 * SS)], fill=GOLD_HI)
    return layer


def _diaper_pattern(w, h):
    """A faint lozenge tapestry pattern for the court panel background."""
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    step = int(26 * SS)
    for yy in range(0, h + step, step):
        for xx in range(0, w + step, step):
            off = (step // 2) if (yy // step) % 2 else 0
            cx, cy = xx + off, yy
            r = step * 0.30
            d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)],
                      outline=(176, 141, 74, 44), width=max(1, SS))
    return layer


def make_court(img, suit, rank, w, h, color):
    d = ImageDraw.Draw(img)
    pad_x, pad_y = int(w * 0.165), int(h * 0.150)
    panel_box = [pad_x, pad_y, w - pad_x, h - pad_y]
    pw, ph = panel_box[2] - panel_box[0], panel_box[3] - panel_box[1]

    # Panel ground: a slightly deeper ivory so the monogram panel reads as inset.
    ground = Image.new("RGBA", (pw, ph), (233, 217, 176, 255))
    ground.alpha_composite(_diaper_pattern(pw, ph))
    pmask = Image.new("L", (pw, ph), 0)
    ImageDraw.Draw(pmask).rounded_rectangle([0, 0, pw - 1, ph - 1],
                                            radius=18 * SS, fill=255)
    img.paste(ground, (panel_box[0], panel_box[1]), pmask)

    # Double frame: moulded gold outside, suit hairline inside.
    draw_rounded(d, panel_box, 18 * SS, fill=None, outline=GOLD_DEEP, width=max(1, 4 * SS))
    draw_rounded(d, [panel_box[0] + 2 * SS, panel_box[1] + 2 * SS,
                     panel_box[2] - 2 * SS, panel_box[3] - 2 * SS],
                 16 * SS, fill=None, outline=GOLD_LINE, width=max(1, 2 * SS))
    draw_rounded(d, [panel_box[0] + 7 * SS, panel_box[1] + 7 * SS,
                     panel_box[2] - 7 * SS, panel_box[3] - 7 * SS],
                 13 * SS, fill=None, outline=_shade(color, 1.05), width=max(1, SS))

    # ---- upper half group: crown + inlaid monogram + flanking pips -------------
    label = RANK_LABEL[rank]
    half = Image.new("RGBA", (pw, ph // 2), (0, 0, 0, 0))

    crown = _crown_layer(rank, int(pw * 0.34), color)
    half.alpha_composite(crown, ((pw - crown.width) // 2, int(ph * 0.045)))

    letter_px = int(96 * SS)
    inlay = serif_text_layer(label, letter_px, GOLD_LINE)
    face = serif_text_layer(label, letter_px, color)
    lx = (pw - face.width) // 2
    ly = int(ph * 0.045) + crown.height + int(2 * SS)
    for ox, oy in ((-2 * SS, -2 * SS), (2 * SS, 2 * SS),
                   (-2 * SS, 2 * SS), (2 * SS, -2 * SS)):
        half.alpha_composite(inlay, (lx + int(ox), ly + int(oy)))
    half.alpha_composite(face, (lx, ly))

    side_pip = pip_layer(suit, 15 * SS, color, ss=SS)
    py = ly + face.height // 2 - side_pip.height // 2
    half.alpha_composite(side_pip, (int(pw * 0.115) - side_pip.width // 2, py))
    half.alpha_composite(side_pip, (int(pw * 0.885) - side_pip.width // 2, py))

    img.alpha_composite(half, (panel_box[0], panel_box[1]))
    img.alpha_composite(half.rotate(180), (panel_box[0], panel_box[1] + ph - ph // 2))

    # Centre divider with a pip medallion over it.
    mid_y = panel_box[1] + ph // 2
    d.line([panel_box[0] + 10 * SS, mid_y, panel_box[2] - 10 * SS, mid_y],
           fill=GOLD_LINE, width=max(1, 2 * SS))
    med_r = int(21 * SS)
    d.ellipse([w / 2 - med_r, mid_y - med_r, w / 2 + med_r, mid_y + med_r],
              fill=(238, 224, 186, 255), outline=GOLD_LINE, width=max(1, 2 * SS))
    draw_pip(img, suit, w / 2, mid_y + 1 * SS, 13 * SS, color)


def make_ace(img, suit, w, h, color):
    d = ImageDraw.Draw(img)
    cx, cy = w / 2, h / 2

    # Gold double ring behind the pip — grandest on the spade, per tradition.
    grand = suit == "Spades"
    r_out = (118 if grand else 104) * SS
    d.ellipse([cx - r_out, cy - r_out, cx + r_out, cy + r_out],
              outline=GOLD_LINE, width=max(1, 3 * SS))
    d.ellipse([cx - r_out + 6 * SS, cy - r_out + 6 * SS,
               cx + r_out - 6 * SS, cy + r_out - 6 * SS],
              outline=_shade(GOLD_LINE, 1.18), width=max(1, SS))

    # Four small corner flourishes on the ring's diagonals.
    for ang in (45, 135, 225, 315):
        a = math.radians(ang)
        fx, fy = cx + math.cos(a) * (r_out + 12 * SS), cy + math.sin(a) * (r_out + 12 * SS)
        fr = 5 * SS
        d.ellipse([fx - fr, fy - fr, fx + fr, fy + fr], fill=GOLD_LINE)
        draw_tiny = 2 * SS
        d.ellipse([fx - draw_tiny, fy - draw_tiny, fx + draw_tiny, fy + draw_tiny],
                  fill=_shade(GOLD_LINE, 1.3))

    draw_pip(img, suit, cx, cy, (96 if grand else 88) * SS, color)

    if grand:
        banner = serif_text_layer("ACE", int(17 * SS), GOLD_DEEP, tracking=int(6 * SS))
        img.alpha_composite(banner, (int(cx - banner.width / 2),
                                     int(cy + r_out + 16 * SS)))


def make_card(suit, rank):
    w, h = CARD_W * SS, CARD_H * SS
    img = card_plate(w, h)
    color = SUIT_COLOR[suit]

    # --- corner indices -----------------------------------------------------
    corner = corner_index(suit, rank, color)
    img.alpha_composite(corner, (int(17 * SS), int(15 * SS)))
    img.alpha_composite(corner.rotate(180, expand=True),
                        (w - corner.width - int(17 * SS), h - corner.height - int(15 * SS)))

    # --- centre --------------------------------------------------------------
    if rank in LAYOUTS:
        for fx, fy in LAYOUTS[rank]:
            draw_pip(img, suit, fx * w, fy * h, 33 * SS, color, inverted=fy > 0.52)
    elif rank == 1:
        make_ace(img, suit, w, h, color)
    else:
        make_court(img, suit, rank, w, h, color)

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
    f1 = load_font(46, serif=False)
    f2 = load_font(28, serif=False)
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

    print(f"Wrote {count} card faces to {CARDS_DIR}")
    print(f"Wrote table felt to {TABLE_DIR}")


if __name__ == "__main__":
    main()
