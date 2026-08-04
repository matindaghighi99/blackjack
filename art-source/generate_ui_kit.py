#!/usr/bin/env python3
"""
Generates the gold casino UI kit: frames, buttons, icons, emblems and title art.

The palette is sampled from docs/screenshots/*.png so the generated kit matches the
concept art rather than approximating it. Key finding from that sampling: the mockup
buttons are DARK jewel-tone fills with bright gold borders, not saturated fills.

    python3 art-source/generate_ui_kit.py

Output: Assets/Art/UI/*.png

Nine-slice: a filename ending in ".b<N>" declares an N-pixel sprite border, which
ArtImportSettings reads and applies on import. e.g. button_green.b48.png
"""

import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "Art", "UI")

SS = 2  # supersampling

# ---------------------------------------------------------------------------
# Palette (sampled from docs/screenshots/main-menu.png)
# ---------------------------------------------------------------------------

FELT = (14, 42, 24)
FILL_GREEN = (14, 35, 17)
FILL_BLUE = (9, 19, 28)
FILL_RED = (32, 9, 9)
FILL_DARK = (8, 15, 13)

# Metallic gold ramp, top to bottom. The dark band at ~0.62 is what reads as "metal".
GOLD_STOPS = [
    (0.00, (255, 246, 206)),
    (0.16, (242, 216, 138)),
    (0.42, (206, 161, 72)),
    (0.60, (146, 102, 34)),
    (0.74, (224, 188, 114)),
    (1.00, (245, 227, 168)),
]
GOLD_MID = (206, 161, 72)
GOLD_LIGHT = (245, 224, 155)
GOLD_DARK = (92, 62, 18)
INK = (240, 236, 222)

DISPLAY_FONT = "/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrebonum-bold.otf"
BODY_FONT = "/usr/share/fonts/truetype/lato/Lato-Bold.ttf"
BODY_FONT_REG = "/usr/share/fonts/truetype/lato/Lato-Regular.ttf"


def font(path, size):
    return ImageFont.truetype(path, size)


# ---------------------------------------------------------------------------
# Gradient helpers
# ---------------------------------------------------------------------------

def _lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def ramp_color(stops, t):
    t = max(0.0, min(1.0, t))
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t0 <= t <= t1:
            span = (t1 - t0) or 1.0
            return _lerp(c0, c1, (t - t0) / span)
    return stops[-1][1]


def vgrad(size, stops):
    """Vertical gradient image."""
    w, h = size
    strip = Image.new("RGB", (1, h))
    for y in range(h):
        strip.putpixel((0, y), ramp_color(stops, y / max(1, h - 1)))
    return strip.resize((w, h), Image.BILINEAR)


def rounded_mask(size, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1],
                                        radius=radius, fill=255)
    return m


def shrink_mask(size, radius, inset):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(
        [inset, inset, size[0] - 1 - inset, size[1] - 1 - inset],
        radius=max(1, radius - inset), fill=255)
    return m


# ---------------------------------------------------------------------------
# Core widget builders
# ---------------------------------------------------------------------------

def gold_frame_panel(w, h, radius, fill, border=9, inner_line=True, fill_alpha=255):
    """
    A rounded panel with a metallic gold border and a dark fill — the building
    block for every button, pill and card in the kit.
    """
    W, H, R, B = w * SS, h * SS, radius * SS, border * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    outer = rounded_mask((W, H), R)
    gold = vgrad((W, H), GOLD_STOPS).convert("RGBA")
    img.paste(gold, (0, 0), outer)

    inner = shrink_mask((W, H), R, B)
    body = Image.new("RGBA", (W, H), fill + (fill_alpha,))
    # Subtle vertical lift so the fill isn't dead flat.
    lift = vgrad((W, H), [(0.0, tuple(min(255, c + 26) for c in fill)),
                          (0.55, fill),
                          (1.0, tuple(max(0, c - 8) for c in fill))]).convert("RGBA")
    body = Image.blend(body, lift, 0.85)
    body.putalpha(fill_alpha)
    img.paste(body, (0, 0), inner)

    if inner_line:
        # Thin bright hairline just inside the gold — the mockups all have this.
        d = ImageDraw.Draw(img)
        d.rounded_rectangle(
            [B + 2 * SS, B + 2 * SS, W - 1 - B - 2 * SS, H - 1 - B - 2 * SS],
            radius=max(1, R - B - 2 * SS), outline=GOLD_LIGHT + (90,), width=max(1, SS))

    return img.resize((w, h), Image.LANCZOS)


def gold_text(text, fnt, tracking=0, stroke=0, bevel=True, flat=False):
    """
    Text filled with the metallic ramp, dark-outlined, with a top bevel.

    `flat` swaps in a two-stop ramp. The full metallic ramp has a dark band through
    its middle which reads as brushed metal at display sizes but turns small text
    to mud, so anything under ~40px should be flat.
    """
    stops = [(0.0, (252, 236, 178)), (1.0, (214, 174, 92))] if flat else GOLD_STOPS
    pad = 26
    probe = ImageDraw.Draw(Image.new("L", (8, 8)))

    if tracking:
        widths = [probe.textlength(ch, font=fnt) for ch in text]
        total = int(sum(widths) + tracking * (len(text) - 1))
        asc, desc = fnt.getmetrics()
        W, H = total + pad * 2, asc + desc + pad * 2

        def render(draw, fill, sw=0, sf=None):
            x = pad
            for ch, cw in zip(text, widths):
                draw.text((x, pad), ch, font=fnt, fill=fill, stroke_width=sw, stroke_fill=sf)
                x += cw + tracking
    else:
        bb = probe.textbbox((0, 0), text, font=fnt, stroke_width=stroke)
        W, H = bb[2] - bb[0] + pad * 2, bb[3] - bb[1] + pad * 2
        ox, oy = pad - bb[0], pad - bb[1]

        def render(draw, fill, sw=0, sf=None):
            draw.text((ox, oy), text, font=fnt, fill=fill, stroke_width=sw, stroke_fill=sf)

    core = Image.new("L", (W, H), 0)
    render(ImageDraw.Draw(core), 255)

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    if stroke:
        halo = Image.new("L", (W, H), 0)
        render(ImageDraw.Draw(halo), 255, stroke, 255)
        out.paste(Image.new("RGBA", (W, H), (46, 30, 6, 255)), (0, 0), halo)

    out.paste(vgrad((W, H), stops).convert("RGBA"), (0, 0), core)

    if bevel:
        # Lit rim along the top of each stroke, shadowed rim along the bottom.
        up = core.transform((W, H), Image.AFFINE, (1, 0, 0, 0, 1, 3 * SS))
        out.paste(Image.new("RGBA", (W, H), (255, 250, 224, 190)), (0, 0), _and_not(core, up))

        down = core.transform((W, H), Image.AFFINE, (1, 0, 0, 0, 1, -3 * SS))
        out.paste(Image.new("RGBA", (W, H), (86, 56, 14, 170)), (0, 0), _and_not(core, down))

    return out


def _and_not(a, b):
    """Pixels present in mask `a` but not in mask `b` — a one-sided rim."""
    inv = b.point(lambda v: 255 - v)
    return Image.composite(a, Image.new("L", a.size, 0), inv)


# ---------------------------------------------------------------------------
# Icons — drawn as vector shapes so they stay crisp and need no font coverage
# ---------------------------------------------------------------------------

def _icon_canvas(d):
    return Image.new("RGBA", (d * SS, d * SS), (0, 0, 0, 0))


def _gold_fill(img, shape_mask):
    g = vgrad(img.size, GOLD_STOPS).convert("RGBA")
    img.paste(g, (0, 0), shape_mask)
    return img


def icon_gift(d=96):
    img = _icon_canvas(d)
    S = d * SS
    m = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(m)
    k.rounded_rectangle([S * .12, S * .38, S * .88, S * .90], radius=S * .06, fill=255)
    k.rounded_rectangle([S * .06, S * .26, S * .94, S * .44], radius=S * .05, fill=255)
    k.ellipse([S * .20, S * .10, S * .50, S * .34], fill=255)
    k.ellipse([S * .50, S * .10, S * .80, S * .34], fill=255)
    cut = ImageDraw.Draw(m)
    cut.ellipse([S * .28, S * .16, S * .46, S * .30], fill=0)
    cut.ellipse([S * .54, S * .16, S * .72, S * .30], fill=0)
    cut.rectangle([S * .44, S * .26, S * .56, S * .92], fill=0)
    return _gold_fill(img, m).resize((d, d), Image.LANCZOS)


def icon_settings(d=96):
    img = _icon_canvas(d)
    S = d * SS
    m = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(m)
    cx = cy = S / 2
    for i in range(8):
        a = math.radians(i * 45)
        k.polygon([
            (cx + math.cos(a - .16) * S * .30, cy + math.sin(a - .16) * S * .30),
            (cx + math.cos(a + .16) * S * .30, cy + math.sin(a + .16) * S * .30),
            (cx + math.cos(a + .11) * S * .46, cy + math.sin(a + .11) * S * .46),
            (cx + math.cos(a - .11) * S * .46, cy + math.sin(a - .11) * S * .46),
        ], fill=255)
    k.ellipse([cx - S * .32, cy - S * .32, cx + S * .32, cy + S * .32], fill=255)
    k.ellipse([cx - S * .14, cy - S * .14, cx + S * .14, cy + S * .14], fill=0)
    return _gold_fill(img, m).resize((d, d), Image.LANCZOS)


def icon_trophy(d=96):
    img = _icon_canvas(d)
    S = d * SS
    m = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(m)
    k.polygon([(S * .27, S * .12), (S * .73, S * .12), (S * .66, S * .58),
               (S * .34, S * .58)], fill=255)
    k.ellipse([S * .34, S * .48, S * .66, S * .66], fill=255)
    k.rectangle([S * .45, S * .62, S * .55, S * .76], fill=255)
    k.rounded_rectangle([S * .28, S * .76, S * .72, S * .88], radius=S * .04, fill=255)
    for sx in (-1, 1):
        cx = S * .5 + sx * S * .30
        k.ellipse([cx - S * .13, S * .16, cx + S * .13, S * .40], fill=255)
        k.ellipse([cx - S * .06, S * .21, cx + S * .06, S * .35], fill=0)
    return _gold_fill(img, m).resize((d, d), Image.LANCZOS)


def icon_cart(d=96):
    img = _icon_canvas(d)
    S = d * SS
    m = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(m)
    k.line([(S * .06, S * .18), (S * .22, S * .18), (S * .34, S * .62)],
           fill=255, width=int(S * .09))
    k.polygon([(S * .26, S * .28), (S * .94, S * .28), (S * .84, S * .60),
               (S * .36, S * .60)], fill=255)
    k.polygon([(S * .34, S * .34), (S * .86, S * .34), (S * .79, S * .54),
               (S * .40, S * .54)], fill=0)
    k.ellipse([S * .36, S * .72, S * .54, S * .90], fill=255)
    k.ellipse([S * .68, S * .72, S * .86, S * .90], fill=255)
    return _gold_fill(img, m).resize((d, d), Image.LANCZOS)


def icon_cards(d=96):
    """Two fanned cards. The front card is drawn over a dark rim so the pair reads
    as two overlapping cards rather than one gold blob."""
    img = _icon_canvas(d)
    S = d * SS

    def card(box, angle, pivot):
        m = Image.new("L", (S, S), 0)
        ImageDraw.Draw(m).rounded_rectangle(box, radius=S * .08, fill=255)
        return m.rotate(angle, resample=Image.BICUBIC, center=pivot)

    back = card([S * .08, S * .20, S * .52, S * .86], -17, (S * .30, S * .53))
    front_rim = card([S * .38, S * .10, S * .92, S * .88], 10, (S * .65, S * .49))
    front = card([S * .43, S * .15, S * .87, S * .83], 10, (S * .65, S * .49))

    _gold_fill(img, back)
    img.paste(Image.new("RGBA", (S, S), (18, 26, 18, 255)), (0, 0), front_rim)
    _gold_fill(img, front)
    return img.resize((d, d), Image.LANCZOS)


def icon_plus(d=96):
    img = _icon_canvas(d)
    S = d * SS
    m = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(m)
    k.rounded_rectangle([S * .43, S * .16, S * .57, S * .84], radius=S * .05, fill=255)
    k.rounded_rectangle([S * .16, S * .43, S * .84, S * .57], radius=S * .05, fill=255)
    return _gold_fill(img, m).resize((d, d), Image.LANCZOS)


def icon_chevron(d=96):
    img = _icon_canvas(d)
    S = d * SS
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).line([(S * .34, S * .18), (S * .68, S * .50), (S * .34, S * .82)],
                           fill=255, width=int(S * .13), joint="curve")
    return _gold_fill(img, m).resize((d, d), Image.LANCZOS)


def spade_mask(S, cx, cy, s):
    """
    Point-up spade. Proportions matter here: too wide and the lobes swallow the
    triangle, which reads as a goblet rather than a spade.
    """
    m = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(m)
    r = s * 0.285
    for sx in (-1, 1):
        ccx = cx + sx * s * 0.265
        ccy = cy + s * 0.155
        k.ellipse([ccx - r, ccy - r, ccx + r, ccy + r], fill=255)
    k.polygon([(cx - s * .55, cy + s * .10), (cx + s * .55, cy + s * .10),
               (cx, cy - s * .70)], fill=255)
    k.polygon([(cx - s * .075, cy + s * .18), (cx + s * .075, cy + s * .18),
               (cx + s * .27, cy + s * .70), (cx - s * .27, cy + s * .70)], fill=255)
    return m


def icon_spade(d=96):
    img = _icon_canvas(d)
    S = d * SS
    return _gold_fill(img, spade_mask(S, S / 2, S / 2, S * .46)).resize((d, d), Image.LANCZOS)


def icon_coin(d=96):
    img = _icon_canvas(d)
    S = d * SS
    ring = Image.new("L", (S, S), 0)
    k = ImageDraw.Draw(ring)
    k.ellipse([S * .04, S * .04, S * .96, S * .96], fill=255)
    _gold_fill(img, ring)
    d2 = ImageDraw.Draw(img)
    d2.ellipse([S * .16, S * .16, S * .84, S * .84], fill=(58, 40, 10, 255))
    inner = Image.new("L", (S, S), 0)
    ImageDraw.Draw(inner).ellipse([S * .20, S * .20, S * .80, S * .80], outline=255,
                                  width=int(S * .04))
    _gold_fill(img, inner)
    img.paste(vgrad((S, S), GOLD_STOPS).convert("RGBA"), (0, 0),
              spade_mask(S, S / 2, S * .52, S * .26))
    return img.resize((d, d), Image.LANCZOS)


# ---------------------------------------------------------------------------
# Composite pieces
# ---------------------------------------------------------------------------

def circle_button(d=120):
    img = Image.new("RGBA", (d * SS, d * SS), (0, 0, 0, 0))
    S = d * SS
    ring = Image.new("L", (S, S), 0)
    ImageDraw.Draw(ring).ellipse([1, 1, S - 2, S - 2], fill=255)
    _gold_fill(img, ring)
    ImageDraw.Draw(img).ellipse([S * .075, S * .075, S * .925, S * .925],
                                fill=FILL_DARK + (245,))
    return img.resize((d, d), Image.LANCZOS)


def emblem_ace(d=520):
    """The Ace-of-Spades crest from the main-menu mockup."""
    S = d * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    cy = S * 0.52
    body = spade_mask(S, S / 2, cy, S * .47)
    _gold_fill(img, body)

    # Thin gold rim, dark face — matches the crest in the concept art.
    inner = spade_mask(S, S / 2, cy, S * .425)
    img.paste(Image.new("RGBA", (S, S), (15, 21, 15, 255)), (0, 0), inner)

    rim = _and_not(spade_mask(S, S / 2, cy, S * .385),
                   spade_mask(S, S / 2, cy, S * .370))
    img.paste(Image.new("RGBA", (S, S), GOLD_MID + (140,)), (0, 0), rim)

    # "A" sits on the body of the spade, not up on the point.
    a = gold_text("A", font(DISPLAY_FONT, int(S * .40)), stroke=int(S * .014))
    img.alpha_composite(a, (int(S / 2 - a.width / 2), int(cy - a.height / 2 - S * .01)))
    return img.resize((d, d), Image.LANCZOS)


def divider(w=760, h=48):
    S = SS
    img = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    W, H = w * S, h * S
    cy = H // 2
    for side in (-1, 1):
        x0 = W / 2 + side * W * .06
        x1 = W / 2 + side * W * .48
        d.line([(x0, cy), (x1, cy)], fill=GOLD_MID + (220,), width=max(1, 2 * S))
        d.line([(x0, cy + 3 * S), (x1 - W * .06, cy + 3 * S)],
               fill=GOLD_DARK + (120,), width=max(1, S))
    r = H * .22
    d.polygon([(W / 2, cy - r), (W / 2 + r * .7, cy), (W / 2, cy + r), (W / 2 - r * .7, cy)],
              fill=GOLD_LIGHT + (235,))
    return img.resize((w, h), Image.LANCZOS)


def card_shadow(w=240, h=320, radius=30, blur=22):
    """Soft drop shadow sat behind each card so hands read as physical objects."""
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    b = blur * SS
    d.rounded_rectangle([b, b, w * SS - b, h * SS - b], radius=radius * SS,
                        fill=(0, 0, 0, 255))
    img = img.filter(ImageFilter.GaussianBlur(b * 0.55))
    return img.resize((w, h), Image.LANCZOS)


def menu_felt(w=1080, h=1920):
    """Richer felt for the menu: pooled light, vignette, corner ornament arcs."""
    img = Image.new("RGB", (w, h), FELT)
    d = ImageDraw.Draw(img)
    cx, cy = w / 2, h * 0.40
    maxr = math.hypot(w, h) * 0.66
    for i in range(150, 0, -1):
        t = i / 150.0
        r = maxr * t
        c = tuple(int(FELT[j] + (34, 44, 30)[j] * (1 - t) ** 1.6) for j in range(3))
        d.ellipse([cx - r, cy - r * .92, cx + r, cy + r * .92], fill=c)

    import random
    random.seed(11)
    n = Image.new("L", (w // 2, h // 2))
    n.putdata([random.randint(116, 140) for _ in range((w // 2) * (h // 2))])
    n = n.resize((w, h), Image.BILINEAR).filter(ImageFilter.GaussianBlur(0.7))
    img = Image.blend(img, Image.merge("RGB", (n, n, n)), 0.05)

    d = ImageDraw.Draw(img, "RGBA")
    d.arc([-w * .30, h * .05, w * 1.30, h * .92], start=203, end=337,
          fill=GOLD_MID + (70,), width=3)
    d.arc([-w * .22, h * .09, w * 1.22, h * .87], start=207, end=333,
          fill=GOLD_MID + (40,), width=2)

    vig = Image.new("L", (w, h), 0)
    ImageDraw.Draw(vig).ellipse([-w * .30, -h * .18, w * 1.30, h * 1.18], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(200))
    return Image.composite(img, Image.new("RGB", (w, h), (4, 14, 9)), vig)


# ---------------------------------------------------------------------------

def main():
    os.makedirs(OUT, exist_ok=True)
    w = 0

    def save(im, name):
        nonlocal w
        im.save(os.path.join(OUT, name + ".png"))
        w += 1

    # Nine-sliced frames. ".bN" tells the importer the sprite border.
    # btn_* frames are no longer drawn here — they are built from the concept render's
    # own gold border by art-source/extract_table_art.py, which looks considerably
    # better. gold_frame_panel still backs the pill and panel below.
    save(gold_frame_panel(320, 200, 34, FILL_DARK, border=7, fill_alpha=210), "panel.b48")
    save(gold_frame_panel(320, 120, 58, FILL_DARK, border=7), "pill.b58")

    save(circle_button(120), "circle_button")
    save(card_shadow(), "card_shadow.b64")

    for fn, nm in ((icon_gift, "icon_gift"), (icon_settings, "icon_settings"),
                   (icon_trophy, "icon_trophy"), (icon_cart, "icon_cart"),
                   (icon_cards, "icon_cards"), (icon_plus, "icon_plus"),
                   (icon_chevron, "icon_chevron"), (icon_spade, "icon_spade")):
        save(fn(96), nm)
    save(icon_coin(112), "icon_coin")

    save(emblem_ace(520), "emblem_ace")
    save(divider(760, 48), "divider")

    # NOTE: titles and labels are NOT baked any more. They are live TextMeshPro text
    # using the gold material built by Assets/Editor/FontAssetBuilder.cs — sharp at any
    # size and editable without regenerating art. gold_text() is kept because the layout
    # preview still uses it.

    save(menu_felt(), "felt_menu")

    print(f"Wrote {w} UI pieces to {OUT}")


if __name__ == "__main__":
    main()
