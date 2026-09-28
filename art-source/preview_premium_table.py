#!/usr/bin/env python3
"""
Design reference for the premium table, rendered from the real art and fonts.

Two things are mirrored here so the render shows what Unity will draw:

* Layout — class Layout is a line-for-line port of
  Assets/Scripts/UI/Layout/TableLayout.cs (safe-area space, origin top-left, y down).
* Look — sizes, fonts, colours and opacities follow Assets/Editor/SceneBootstrap.cs and
  the runtime components (TotalBadge, ResultBanner, FeltArc, BetChipView…).

Keep both in step when either side changes. Layout has to be judged by eye across very
different screens and states, and a Unity round-trip per tweak is slow.

    python3 art-source/preview_premium_table.py [out_dir]      # default art-source/previews/
    DEV=phone,desktop STATE=split python3 art-source/preview_premium_table.py
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

from premium_common import ART, FONTS, HERE  # noqa: E402

# ---------------------------------------------------------------------------
# Palette — mirrors Palette.cs
# ---------------------------------------------------------------------------

INK = (11, 13, 12)
GLASS = (14, 17, 16)
GOLD = (201, 164, 92)
GOLD_LIGHT = (231, 207, 150)
IVORY = (243, 235, 221)
MUTED = (150, 158, 153)
FAINT = (104, 112, 107)
WIN = (134, 201, 160)
LOSS = (214, 124, 112)
PUSH = (191, 197, 193)
HUD_BAND = (8, 10, 9)

SERIF = os.path.join(FONTS, "EBGaramond-Regular-Lining.otf")
SERIF_ITALIC = os.path.join(FONTS, "EBGaramond-Italic-Lining.otf")
SANS_MED = os.path.join(FONTS, "Inter-Medium-Tabular.otf")
SANS_SEMI = os.path.join(FONTS, "Inter-SemiBold-Tabular.otf")

_fonts = {}
_images = {}


def F(path, size):
    key = (path, int(round(size)))
    if key not in _fonts:
        _fonts[key] = ImageFont.truetype(path, max(6, int(round(size))))
    return _fonts[key]


def art(rel):
    if rel not in _images:
        _images[rel] = Image.open(os.path.join(ART, rel)).convert("RGBA")
    return _images[rel]


# ---------------------------------------------------------------------------
# Layout — port of TableLayout.cs
# ---------------------------------------------------------------------------

CARD_ASPECT = 360 / 504
PORTRAIT_THRESHOLD = 0.9
SHOE_SCALE = 0.55


def clamp(v, lo, hi):
    return max(lo, min(hi, v))


def card_width(h):
    return h * CARD_ASPECT


def max_span(h):
    return h * CARD_ASPECT * 2.9


class Layout:
    def __init__(self, width, height):
        self.W = max(1.0, width)
        self.H = max(1.0, height)
        self.portrait = self.H / self.W >= PORTRAIT_THRESHOLD
        self.cx = self.W / 2

        self.hud_h = 112 if self.portrait else 100
        self.hud_y = self.hud_h / 2
        self.show_session = (not self.portrait) and self.W >= 1500

        self.control_h = 116 if self.portrait else 108
        self.control_y = self.H - 30 - self.control_h / 2
        if self.portrait:
            self.strip_h = 64
            self.strip_y = self.control_y - self.control_h / 2 - 22 - self.strip_h / 2
            self.dock_top = self.strip_y - self.strip_h / 2 - 18
            self.control_w = min(self.W - 48, 980)
        else:
            self.strip_h = self.control_h
            self.strip_y = self.control_y
            self.dock_top = self.control_y - self.control_h / 2 - 26
            self.control_w = min(self.W - 800, 900)

        self.rail_thick = 46 if self.portrait else 42
        self.rail_sag = 70 if self.portrait else 80
        self.rail_y = self.dock_top - self.rail_thick / 2

        self.t_top = self.hud_h + 8
        self.t_bottom = self.rail_y - self.rail_thick / 2 - (10 if self.portrait else 30)
        th = self.t_bottom - self.t_top

        if self.portrait:
            self.dealer_h = clamp(th * 0.205, 160, 310)
            self.player_h = clamp(th * 0.245, 180, 360)
            dly = self.t_top + 38
            self.dealer_label = (self.cx, dly)
            self.dealer_label_idle = self.dealer_label
            self.dealer_y = dly + 36 + self.dealer_h / 2
            bet_y = self.t_bottom - 64
            self.bet = (self.cx, bet_y)
            self.player_y = bet_y - 92 - self.player_h / 2
            self.shoe = (self.W - 100, self.t_top + 92)
            gap_top = self.dealer_y + self.dealer_h / 2
            gap_bottom = self.player_y - self.player_h / 2 - 36 - 24
            dw = card_width(self.dealer_h)
            shoe_left = self.shoe[0] - card_width(self.dealer_h * SHOE_SCALE) / 2 - 12
            self.dealer_span = max(dw * 0.6, min(max_span(self.dealer_h), 2 * (shoe_left - 16 - self.cx) - dw))
            pw = card_width(self.player_h)
            self.player_span = max(pw * 0.6, min(max_span(self.player_h), self.W - 48 - pw))
        else:
            self.dealer_h = clamp(th * 0.28, 150, 250)
            self.player_h = clamp(th * 0.33, 170, 290)
            self.dealer_y = self.t_top + 20 + self.dealer_h / 2
            self.player_y = self.t_bottom - 18 - self.player_h / 2
            self.dealer_span = max_span(self.dealer_h)
            self.dealer_label = (self.cx - (self.dealer_span / 2 + card_width(self.dealer_h) / 2 + 110), self.dealer_y)
            self.dealer_label_idle = (self.cx, self.dealer_y)
            pw = card_width(self.player_h)
            self.player_span = pw * 2.4
            bet_x = self.cx + self.player_span / 2 + pw / 2 + 150
            if bet_x > self.W - 150:
                bet_x = self.W - 150
                self.player_span = max(pw * 0.6, 2 * (bet_x - 150 - self.cx) - pw)
            self.bet = (bet_x, self.player_y + self.player_h * 0.18)
            self.shoe = (self.W - 150, self.t_top + 120)
            gap_top = self.dealer_y + self.dealer_h / 2
            gap_bottom = self.player_y - self.player_h / 2 - 12

        self.print_y = (gap_top + gap_bottom) / 2
        self.gap_h = gap_bottom - gap_top
        self.banner_y = self.print_y
        self.banner_h = clamp(self.gap_h - 12, 120, 176)

    show_print = property(lambda s: s.gap_h >= 70)
    show_print2 = property(lambda s: s.gap_h >= 120)
    print1_y = property(lambda s: s.print_y - 20 if s.show_print2 else s.print_y)
    print2_y = property(lambda s: s.print_y + 30)
    print1_size = property(lambda s: 40 if s.portrait else 36)
    print2_size = property(lambda s: 30 if s.portrait else 28)
    print_radius = property(lambda s: s.W * (1.25 if s.portrait else 1.9))
    rail_half_chord = property(lambda s: s.W / 2 * 1.08)
    rail_radius = property(lambda s: (s.rail_half_chord ** 2 + s.rail_sag ** 2) / (2 * s.rail_sag))
    banner_scale = property(lambda s: s.banner_h / 176)
    wordmark_size = property(lambda s: 40 if s.portrait else 38)
    control_left = property(lambda s: s.cx - s.control_w / 2)

    def seats(self, count):
        pw_full = card_width(self.player_h)
        if count <= 1:
            span = self.player_span
            badge = (0, -self.player_h / 2 - 36) if self.portrait else (-(span / 2 + pw_full / 2 + 110), 0)
            return dict(xs=[self.cx], card_h=self.player_h, span=span, badge=badge, bet=self.bet)
        if self.portrait:
            shrink = 0.86 if count == 2 else 0.72 if count == 3 else 0.56
            usable, centre = self.W - 60, self.cx
        else:
            shrink = 0.9 if count == 2 else 0.8 if count == 3 else 0.72
            usable, centre = min(self.W - 120 - 320, 1300), self.cx - 140
        spacing = usable / count
        ch = min(self.player_h * shrink, (spacing - 24) / 1.52 / CARD_ASPECT)
        xs = [centre + (i - (count - 1) / 2) * spacing for i in range(count)]
        bet = self.bet if self.portrait else (xs[-1] + spacing / 2 + 150, self.bet[1])
        return dict(xs=xs, card_h=ch, span=max(0.0, spacing - card_width(ch) - 24), badge=(0, -ch / 2 - 30), bet=bet)

    def bet_row(self, n):
        gap = 12
        deal_w = self.control_w * 0.28
        clear_w = self.control_h * 0.62
        d = min(self.control_h * 0.92, (self.control_w - deal_w - clear_w - 30 - n * gap) / n)
        slots, x = [], self.control_left + d / 2
        for _ in range(n):
            slots.append((x, d, d))
            x += d + gap
        deal_x = self.control_left + self.control_w - deal_w / 2
        slots.append((deal_x - deal_w / 2 - 18 - clear_w / 2, clear_w, clear_w))
        slots.append((deal_x, deal_w, self.control_h))
        return slots

    def action_row(self, surrender=False):
        gap = 14
        count = 5 if surrender else 4
        usable = self.control_w - gap * (count - 1)
        sec = usable * (0.16 if surrender else 0.2)
        pri = usable * (0.26 if surrender else 0.3)
        widths = [sec, pri, pri, sec] + ([sec] if surrender else [])
        slots, x = [], self.control_left
        for i, w in enumerate(widths):
            slots.append((x + w / 2, w, self.control_h if i in (1, 2) else self.control_h * 0.9))
            x += w + gap
        return slots

    def strip_items(self):
        if self.portrait:
            step = self.control_w * 0.33
            return [(self.cx - step, self.strip_y, "c"), (self.cx, self.strip_y, "c"), (self.cx + step, self.strip_y, "c")]
        return [(40, self.strip_y, "l"), (self.W - 210, self.strip_y, "r"), (self.W - 40, self.strip_y, "r")]


# ---------------------------------------------------------------------------
# Drawing primitives
# ---------------------------------------------------------------------------

class Canvas:
    """A compositing surface in canvas units; (ox, oy) is the safe area's top-left."""

    def __init__(self, w, h, ox=0, oy=0):
        self.img = Image.new("RGBA", (int(w), int(h)), INK + (255,))
        self.ox, self.oy = ox, oy

    def paste(self, im, x, y):
        self.img.alpha_composite(im, (int(round(x + self.ox - im.width / 2)), int(round(y + self.oy - im.height / 2))))


def tint(im, colour, alpha=1.0):
    out = Image.new("RGBA", im.size, colour + (0,))
    out.putalpha(im.split()[3].point(lambda v: int(v * alpha)))
    return out


def nine(rel, border, w, h, colour=None, alpha=1.0, radius=None):
    """Nine-slice like a Unity Sliced Image; `radius` sets the drawn border
    (pixelsPerUnitMultiplier = border / radius)."""
    src = art(rel)
    w, h = max(2, int(round(w))), max(2, int(round(h)))
    bd = int(round(radius)) if radius is not None else border
    bd = max(1, min(bd, w // 2, h // 2))
    sw, sh = src.size
    xs, ys = [0, border, sw - border, sw], [0, border, sh - border, sh]
    xd, yd = [0, bd, w - bd, w], [0, bd, h - bd, h]
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for i in range(3):
        for j in range(3):
            dw, dh = xd[i + 1] - xd[i], yd[j + 1] - yd[j]
            if dw > 0 and dh > 0:
                piece = src.crop((xs[i], ys[j], xs[i + 1], ys[j + 1])).resize((dw, dh), Image.LANCZOS)
                out.alpha_composite(piece, (xd[i], yd[j]))
    return tint(out, colour, alpha) if colour is not None else out


def text_width(s, font, tracking):
    return sum(font.getlength(ch) for ch in s) + tracking * max(0, len(s) - 1)


def draw_text(c, s, x, y, font, colour, tracking=0.0, anchor="c", alpha=1.0, gradient=None):
    """Text with manual tracking; y is the visual centre of the capitals."""
    if not s:
        return 0
    layer = Image.new("RGBA", c.img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    width = text_width(s, font, tracking)
    cap = font.getbbox("H")
    mid = (cap[1] + cap[3]) / 2
    left = x - width / 2 if anchor == "c" else x if anchor == "l" else x - width
    cx = left + c.ox
    fill = (255, 255, 255) if gradient else colour
    for ch in s:
        d.text((cx, y + c.oy - mid), ch, font=font, fill=fill + (int(255 * alpha),))
        cx += font.getlength(ch) + tracking
    if gradient:
        top, bottom = gradient
        box = layer.getbbox()
        if box:
            grad = Image.new("RGBA", layer.size, (0, 0, 0, 0))
            gd = ImageDraw.Draw(grad)
            for yy in range(box[1], box[3] + 1):
                t = (yy - box[1]) / max(1, box[3] - box[1])
                gd.line((box[0], yy, box[2], yy), fill=tuple(int(top[k] + (bottom[k] - top[k]) * t) for k in range(3)) + (255,))
            grad.putalpha(layer.split()[3])
            layer = grad
    c.img.alpha_composite(layer)
    return width


def draw_arc_text(c, s, x, y, radius, font, colour, tracking=0.0, alpha=1.0):
    """Mirror of CurvedText: each glyph placed on a 'smile' arc and turned to its tangent."""
    width = text_width(s, font, tracking)
    cursor = -width / 2
    cap = font.getbbox("H")
    for ch in s:
        cw = font.getlength(ch)
        a = (cursor + cw / 2) / radius
        g = Image.new("RGBA", (int(cw + 24), int(cap[3] * 2 + 24)), (0, 0, 0, 0))
        ImageDraw.Draw(g).text((12, 12 + (cap[3] - cap[1]) / 2 - cap[1]), ch, font=font, fill=colour + (int(255 * alpha),))
        g = g.rotate(math.degrees(a), resample=Image.BICUBIC, expand=True)
        c.paste(g, x + radius * math.sin(a), y - radius * (1 - math.cos(a)))
        cursor += cw + tracking


def glow(c, x, y, w, h, colour, alpha):
    c.paste(tint(art("UI/ui_glow.png").resize((max(2, int(w)), max(2, int(h))), Image.LANCZOS), colour, alpha), x, y)


# ---------------------------------------------------------------------------
# Components
# ---------------------------------------------------------------------------

def rail(c, lay):
    """FeltArc: the shaded band and the apron, same stops and colours as the component."""
    stops = [(0.0, GOLD), (0.07, GOLD), (0.10, (48, 44, 40)), (0.40, (26, 24, 22)), (0.80, (9, 10, 10)), (1.0, (9, 10, 10))]
    R = lay.rail_radius
    centre = lay.rail_y - R
    layer = Image.new("RGBA", c.img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    reach = min(lay.W / 2 + 800, R * 0.995)
    pts = []
    for i in range(161):
        dx = -reach + 2 * reach * i / 160
        root = math.sqrt(max(0.0, R * R - dx * dx))
        pts.append((lay.cx + dx + dx / R * lay.rail_thick / 2 + c.ox, centre + root + root / R * lay.rail_thick / 2 + c.oy))
    d.polygon(pts + [(pts[-1][0], c.img.height + 10), (pts[0][0], c.img.height + 10)], fill=(10, 12, 11, 255))
    steps = int(lay.rail_thick * 2)
    for k in range(steps + 1):
        f = k / steps
        col = stops[-1][1]
        for i in range(len(stops) - 1):
            if stops[i][0] <= f <= stops[i + 1][0]:
                t = (f - stops[i][0]) / max(1e-6, stops[i + 1][0] - stops[i][0])
                col = tuple(int(stops[i][1][j] + (stops[i + 1][1][j] - stops[i][1][j]) * t) for j in range(3))
                break
        r = R + (f - 0.5) * lay.rail_thick
        cx, cy = lay.cx + c.ox, centre + c.oy
        d.arc((cx - r, cy - r, cx + r, cy + r), 0, 180, fill=col + (255,), width=2)
    c.img.alpha_composite(layer)


def card(c, name, x, y, h, rot=0.0, dim=1.0, shadow=True):
    w = card_width(h)
    face = art(f"Cards/{name}.png").resize((int(w), int(h)), Image.LANCZOS)
    if dim < 1.0:
        dark = Image.new("RGBA", face.size, (0, 0, 0, 0))
        dark.putalpha(face.split()[3].point(lambda v: int(v * (1 - dim))))
        face.alpha_composite(dark)
    if shadow:
        sh = nine("UI/card_shadow.b64.png", 64, w + 44, h + 44, (0, 0, 0), 0.55 * (0.5 + 0.5 * dim))
        c.paste(sh.rotate(rot, resample=Image.BICUBIC, expand=True) if rot else sh, x + 4, y + 9)
    c.paste(face.rotate(rot, resample=Image.BICUBIC, expand=True) if rot else face, x, y)


def hand(c, names, x, y, h, span_cap, dim=1.0, fan=4.0):
    """HandView layout: step 0.62 card widths, capped by the span; fan plus fixed jitter."""
    w = card_width(h)
    n = len(names)
    step = w * 0.62
    if n > 1 and step * (n - 1) > span_cap:
        step = span_cap / (n - 1)
    span = step * (n - 1) if n > 1 else 0
    for i, nm in enumerate(names):
        t = (i / (n - 1) - 0.5) if n > 1 else 0
        z = -t * fan + 1.6 * math.sin(i * 12.9898)
        card(c, nm, x - span / 2 + step * i, y, h, rot=z, dim=dim)
    return span + w


def badge(c, x, y, value, tone="normal", label=None, label_gold=False):
    """TotalBadge: tracked caption beside a pill coloured by tone."""
    lf, vf = F(SANS_MED, 20), F(SANS_SEMI, 28)
    lw = text_width(label, lf, 5) if label else 0
    pw = max(64, text_width(value, vf, 0.5) + 34) if value else 0
    gap = 16 if (label and value) else 0
    left = x - (lw + gap + pw) / 2
    if label:
        draw_text(c, label, left + lw / 2, y, lf, GOLD if label_gold else MUTED, tracking=5)
    if value:
        fill = {"normal": IVORY, "soft": IVORY, "21": GOLD_LIGHT, "blackjack": GOLD, "bust": LOSS,
                "win": WIN, "loss": LOSS, "push": PUSH}[tone]
        c.paste(nine("UI/ui_pill.b64.png", 64, pw, 46, fill, 1.0, radius=23), left + lw + gap + pw / 2, y)
        draw_text(c, value, left + lw + gap + pw / 2, y, vf, INK, tracking=0.5)


def button(c, x, y, w, h, label, style, sub=None, disabled=False, focus=False, glow_on=False):
    a = 0.36 if disabled else 1.0
    if glow_on and not disabled:
        glow(c, x, y, w + 92, h + 92, GOLD, 0.14)
    c.paste(nine("UI/ui_shadow.b100.png", 100, w + 88, h + 88, (0, 0, 0), 0.55 * a), x, y + 8)
    if style in ("gold", "ivory"):
        c.paste(nine("UI/ui_rrect.b48.png", 48, w, h, GOLD if style == "gold" else IVORY, a, radius=20), x, y)
        ink = INK
    else:
        c.paste(nine("UI/ui_rrect.b48.png", 48, w, h, GLASS, 0.86 * a, radius=20), x, y)
        c.paste(nine("UI/ui_rrect_stroke.b48.png", 48, w, h, GOLD, 0.55 * a, radius=20), x, y)
        ink = IVORY
    if focus:
        c.paste(nine("UI/ui_focus_ring.b72.png", 72, w + 24, h + 24, GOLD_LIGHT, 1.0, radius=32), x, y)
    size = 30 if h >= 110 else 26
    if sub is not None:
        draw_text(c, label, x, y - 12, F(SANS_SEMI, size), ink, tracking=3.5, alpha=a)
        draw_text(c, sub, x, y + 22, F(SANS_MED, 19), MUTED if style == "glass" else ink, tracking=1.5, alpha=a)
    else:
        draw_text(c, label, x, y, F(SANS_SEMI, size), ink, tracking=3.5, alpha=a)


def icon_button(c, icon, x, y, d, dot=False, disabled=False, focus=False):
    a = 0.36 if disabled else 1.0
    c.paste(tint(art("UI/ui_circle.png").resize((int(d), int(d)), Image.LANCZOS), GLASS, 0.72 * a), x, y)
    c.paste(tint(art("UI/ui_circle_stroke.png").resize((int(d), int(d)), Image.LANCZOS), IVORY, 0.18 * a), x, y)
    g = int(d * 0.48)
    c.paste(tint(art(f"UI/{icon}.png").resize((g, g), Image.LANCZOS), IVORY, 0.92 * a), x, y)
    if focus:
        c.paste(tint(art("UI/ui_circle_stroke.png").resize((int(d + 16), int(d + 16)), Image.LANCZOS), GOLD_LIGHT, 1.0), x, y)
    if dot:
        c.paste(tint(art("UI/ui_circle.png").resize((14, 14), Image.LANCZOS), GOLD, 1.0), x + d * 0.34, y - d * 0.34)


def chip_label(v):
    if v >= 1000:
        return f"{v // 1000}K" if v % 1000 == 0 else f"{v / 1000:.1f}K"
    return str(v)


def chip_button(c, value, x, y, d, selected=False, disabled=False):
    oy = -9 if selected else 0
    c.paste(art("Chips/chip_shadow.png").resize((int(d * 1.1), int(d * 0.5)), Image.LANCZOS), x, y + d * 0.47)
    if selected:
        c.paste(tint(art("UI/ui_circle_stroke.png").resize((int(d + 16), int(d + 16)), Image.LANCZOS), GOLD, 0.95), x, y + oy)
    face = art(f"Chips/chip_{value}.png").resize((int(d), int(d)), Image.LANCZOS)
    if disabled:
        face = Image.blend(face, Image.new("RGBA", face.size, (0, 0, 0, 0)), 0.64)
    c.paste(face, x, y + oy)
    lab = chip_label(value)
    size = min(30, d * 0.3) if len(lab) <= 2 else min(30, d * 0.25)
    draw_text(c, lab, x, y + oy, F(SANS_SEMI, size), INK if value in (1, 1000, 10000) else IVORY,
              alpha=0.36 if disabled else 1)


def breakdown(amount, denoms, cap=10):
    out = []
    for dn in sorted(denoms, reverse=True):
        while amount >= dn and len(out) < cap:
            out.append(dn)
            amount -= dn
    return out


def chip_pile(c, amount, x, y, denoms, chip_w):
    """BetChipView: side-view chips stepped by 8.2% of their width over a contact shadow."""
    step = chip_w * 0.082
    c.paste(art("Chips/chip_shadow.png").resize((int(chip_w * 1.3), int(chip_w * 0.5)), Image.LANCZOS), x, y + chip_w * 0.16)
    for k, v in enumerate(breakdown(amount, denoms)):
        side = art(f"Chips/chip_{v}_side.png")
        side = side.resize((int(chip_w), int(side.height * chip_w / side.width)), Image.LANCZOS)
        c.paste(side, x, y - k * step)


def readout(c, x, y, caption, value, colour=IVORY, anchor="c"):
    draw_text(c, caption, x, y - 17, F(SANS_MED, 17), FAINT, tracking=4.5, anchor=anchor)
    draw_text(c, value, x, y + 13, F(SANS_SEMI, 32), colour, tracking=0.5, anchor=anchor)


def banner(c, lay, head, amount, detail, tone):
    accent = {"blackjack": GOLD, "win": WIN, "loss": LOSS, "push": PUSH}[tone]
    k, bh = lay.banner_scale, lay.banner_h
    c.paste(tint(art("UI/ui_hairline.png").resize((int(lay.W), int(bh)), Image.LANCZOS), (6, 8, 7), 0.8), lay.cx, lay.banner_y)
    rule = art("UI/ui_hairline.png").resize((int(lay.W * 0.8), 2), Image.LANCZOS)
    ra = 0.4 if tone == "push" else 0.8
    c.paste(tint(rule, accent, ra), lay.cx, lay.banner_y - bh / 2)
    c.paste(tint(rule, accent, ra), lay.cx, lay.banner_y + bh / 2)
    draw_text(c, head, lay.cx, lay.banner_y - 34 * k, F(SERIF, 62 * k), GOLD if tone == "blackjack" else IVORY, tracking=10 * k)
    draw_text(c, amount, lay.cx, lay.banner_y + 18 * k, F(SANS_SEMI, 34 * k), accent, tracking=1 * k)
    draw_text(c, detail, lay.cx, lay.banner_y + 52 * k, F(SANS_MED, 20 * k), MUTED, tracking=2 * k)


# ---------------------------------------------------------------------------
# States
# ---------------------------------------------------------------------------

DENOMS = [10, 25, 100, 500, 1000]

STATES = {
    "empty": dict(bet=0, status="PLACE YOUR BET  ·  MIN 10  ·  MAX 10,000"),
    "betting": dict(bet=500, status="READY TO DEAL", selected=500),
    "playing": dict(bet=500, phase="act", status="YOUR MOVE", balance=11950,
                    dealer=["card_Hearts_10", "card_Back"], dealer_total="10",
                    seats=[(["card_Spades_09", "card_Diamonds_07"], "16", "normal", None)]),
    "soft": dict(bet=100, phase="act", status="YOUR MOVE", balance=12350, can_double=True,
                 dealer=["card_Clubs_06", "card_Back"], dealer_total="6",
                 seats=[(["card_Hearts_01", "card_Spades_06"], "7 / 17", "soft", None)]),
    "split": dict(bet=1000, phase="act", status="HAND 2 OF 2", balance=11450, active=1,
                  dealer=["card_Clubs_06", "card_Back"], dealer_total="6",
                  seats=[(["card_Hearts_08", "card_Spades_03", "card_Clubs_10"], "21", "21", None),
                         (["card_Diamonds_08", "card_Hearts_12"], "18", "normal", None)]),
    "split4": dict(bet=2000, phase="act", status="HAND 3 OF 4", balance=10450, active=2,
                   dealer=["card_Clubs_05", "card_Back"], dealer_total="5",
                   seats=[(["card_Hearts_08", "card_Spades_03", "card_Clubs_10"], "21", "21", None),
                          (["card_Diamonds_08", "card_Hearts_12"], "18", "normal", None),
                          (["card_Clubs_08", "card_Spades_02"], "10", "normal", None),
                          (["card_Spades_08"], "8", "normal", None)]),
    "dealer": dict(bet=500, phase="dealer", status="DEALER'S TURN", balance=11950, dealer_turn=True,
                   dealer=["card_Hearts_10", "card_Spades_06", "card_Clubs_04"], dealer_total="20",
                   seats=[(["card_Spades_09", "card_Diamonds_07", "card_Hearts_03"], "19", "normal", None)]),
    "blackjack": dict(bet=500, status="READY TO DEAL", balance=13200, last=("+750", WIN), selected=500,
                      dealer=["card_Clubs_09", "card_Hearts_08"], dealer_total="17",
                      seats=[(["card_Spades_01", "card_Hearts_13"], "BLACKJACK", "blackjack", "blackjack")],
                      banner=("BLACKJACK", "+750", "Blackjack pays 3 to 2", "blackjack")),
    "win": dict(bet=500, status="READY TO DEAL", balance=12950, last=("+500", WIN), selected=500,
                dealer=["card_Clubs_10", "card_Hearts_06", "card_Spades_09"], dealer_total="BUST", dealer_tone="bust",
                seats=[(["card_Diamonds_10", "card_Hearts_08"], "18", "normal", "win")],
                banner=("YOU WIN", "+500", "Dealer busts with 25", "win")),
    "lose": dict(bet=500, status="READY TO DEAL", balance=11950, last=("−500", LOSS), selected=500,
                 dealer=["card_Diamonds_10", "card_Spades_09"], dealer_total="19",
                 seats=[(["card_Clubs_10", "card_Hearts_07"], "17", "normal", "loss")],
                 banner=("DEALER WINS", "−500", "19 beats 17", "loss")),
    "push": dict(bet=500, status="READY TO DEAL", balance=12450, last=("±0", IVORY), selected=500,
                 dealer=["card_Diamonds_10", "card_Spades_09"], dealer_total="19",
                 seats=[(["card_Clubs_10", "card_Hearts_09"], "19", "normal", "push")],
                 banner=("PUSH", "Bet returned", "Both have 19", "push")),
    "broke": dict(bet=0, balance=5, status="OUT OF CHIPS  ·  VISIT THE STORE", broke=True),
}


def render(dev_w, dev_h, insets, name, out_path):
    st = STATES[name]
    l, t, r, b = insets
    lay = Layout(dev_w - l - r, dev_h - t - b)
    c = Canvas(dev_w, dev_h, l, t)

    # Backdrop, full bleed.
    c.img.alpha_composite(art("Table/room_base.png").resize((dev_w, dev_h), Image.LANCZOS))
    c.img.alpha_composite(art("Table/felt_base.png").resize((dev_w, dev_h), Image.LANCZOS))
    fiber = art("Table/felt_fiber.png")
    fl = Image.new("RGBA", (dev_w, dev_h), (0, 0, 0, 0))
    for yy in range(0, dev_h, fiber.height):
        for xx in range(0, dev_w, fiber.width):
            fl.alpha_composite(fiber, (xx, yy))
    c.img.alpha_composite(fl)
    c.img.alpha_composite(art("Table/vignette.png").resize((dev_w, dev_h), Image.LANCZOS))

    glow(c, lay.cx, (lay.t_top + lay.t_bottom) / 2, lay.W * (1.25 if lay.portrait else 0.95),
         (lay.t_bottom - lay.t_top) * (0.85 if lay.portrait else 1.1), (255, 236, 200), 0.10)
    rail(c, lay)

    banner_spec = st.get("banner")
    if lay.show_print and not banner_spec:
        draw_arc_text(c, "BLACKJACK PAYS 3 TO 2", lay.cx, lay.print1_y, lay.print_radius,
                      F(SERIF, lay.print1_size), GOLD, tracking=8, alpha=0.72)
        if lay.show_print2:
            draw_arc_text(c, "Dealer hits soft 17", lay.cx, lay.print2_y, lay.print_radius,
                          F(SERIF_ITALIC, lay.print2_size), GOLD, tracking=1.5, alpha=0.58)

    seats = st.get("seats", [])
    info = lay.seats(max(1, len(seats)))
    bx, by = info["bet"]
    spot_w = 250 if lay.portrait else 230
    c.paste(tint(art("UI/ui_bet_spot.png").resize((spot_w, int(spot_w * 0.56)), Image.LANCZOS), GOLD, 0.45), bx, by)

    sh = lay.dealer_h * SHOE_SCALE
    for k in range(5):
        card(c, "card_Back", lay.shoe[0] + k * 1.5, lay.shoe[1] - k * 2.2, sh, rot=8, shadow=(k == 0))

    dealer = st.get("dealer", [])
    if dealer:
        hand(c, dealer, lay.cx, lay.dealer_y, lay.dealer_h, lay.dealer_span)
        badge(c, *lay.dealer_label, st.get("dealer_total"), st.get("dealer_tone", "normal"), label="DEALER",
              label_gold=st.get("dealer_turn", False))
    else:
        badge(c, *lay.dealer_label_idle, None, label="DEALER")

    phase = st.get("phase", "bet")
    active = st.get("active", 0)
    split = len(seats) > 1
    for i, (cards_, total, tone, result) in enumerate(seats):
        x = info["xs"][i]
        is_active = phase == "act" and i == active
        dim, h = 1.0, info["card_h"]
        if split and phase == "act" and not is_active:
            dim, h = 0.8, h * 0.92
        if result == "loss":
            dim = 0.62
        width = card_width(h) + card_width(h) * 0.62 * (len(cards_) - 1)
        if result == "blackjack":
            glow(c, x, lay.player_y, (width + card_width(h)) * 1.6, h * 2.4, GOLD, 0.7)
        elif result == "win":
            glow(c, x, lay.player_y, (width + card_width(h)) * 1.6, h * 2.4, WIN, 0.42)
        hand(c, cards_, x, lay.player_y, h, info["span"], dim=dim)
        bdx, bdy = info["badge"]
        badge(c, x + bdx, lay.player_y + bdy, total, tone, label=f"HAND {i + 1}" if split else "YOU", label_gold=is_active)
        if is_active and split:
            mw = card_width(info["card_h"]) * 1.3
            c.paste(tint(art("UI/ui_hairline.png").resize((int(mw), 3), Image.LANCZOS), GOLD, 1.0),
                    x, lay.player_y + info["card_h"] / 2 + 16)

    bet = st.get("bet", 0)
    if bet:
        chip_pile(c, bet, bx, by + 6, DENOMS, 128 if lay.portrait else 120)
    if banner_spec:
        banner(c, lay, *banner_spec)

    draw_text(c, st.get("status", ""), lay.cx, lay.rail_y + 3, F(SANS_MED, 18), IVORY, tracking=5, alpha=0.82)

    # HUD.
    c.img.alpha_composite(Image.new("RGBA", (dev_w, int(lay.hud_h + t)), HUD_BAND + (240,)), (0, 0))
    c.paste(tint(art("UI/ui_hairline.png").resize((int(lay.W * 0.9), 2), Image.LANCZOS), GOLD, 0.5), lay.cx, lay.hud_h)
    icon_button(c, "icon_back", 56, lay.hud_y, 64)
    for i, ic in enumerate(["icon_settings", "icon_stats", "icon_gift"]):
        icon_button(c, ic, lay.W - 56 - i * 80, lay.hud_y, 64, dot=(ic == "icon_gift"))
    draw_text(c, "BLACKJACK", lay.cx, lay.hud_y, F(SERIF, lay.wordmark_size), GOLD, tracking=13,
              gradient=((236, 214, 160), (186, 148, 80)))
    if lay.show_session:
        draw_text(c, "CLASSIC  ·  6 DECKS  ·  10 – 10,000", 112, lay.hud_y, F(SANS_MED, 18), FAINT, tracking=3, anchor="l")

    # Readouts.
    items = lay.strip_items()
    last, last_col = st.get("last", ("—", MUTED))
    readout(c, items[0][0], items[0][1], "BALANCE", f"{st.get('balance', 12450):,}", anchor=items[0][2])
    readout(c, items[1][0], items[1][1], "BET", f"{bet:,}" if bet else "—", anchor=items[1][2])
    readout(c, items[2][0], items[2][1], "LAST", last, last_col, anchor=items[2][2])
    if lay.portrait:
        step = lay.control_w / 6
        for dx in (-step, step):
            div = art("UI/ui_hairline.png").resize((44, 2), Image.LANCZOS).rotate(90, expand=True)
            c.paste(tint(div, IVORY, 0.18), lay.cx + dx, lay.strip_y)
        icon_button(c, "icon_plus", lay.cx - lay.control_w * 0.33 * 0.58, lay.strip_y + 13, 40, dot=st.get("broke", False))
    else:
        icon_button(c, "icon_plus", 252, lay.strip_y + 13, 40, dot=st.get("broke", False))

    # Controls.
    if phase == "bet":
        slots = lay.bet_row(len(DENOMS))
        broke = st.get("broke", False)
        for i, v in enumerate(DENOMS):
            x, w, _ = slots[i]
            chip_button(c, v, x, lay.control_y, w, selected=(v == st.get("selected")), disabled=broke)
        x, w, _ = slots[len(DENOMS)]
        icon_button(c, "icon_undo", x, lay.control_y, 52, disabled=(bet == 0))
        x, w, h = slots[-1]
        button(c, x, lay.control_y, w, h, "DEAL", "gold", disabled=(bet == 0), glow_on=True)
    else:
        slots = lay.action_row()
        acting = phase == "act"
        per_hand = bet // max(1, len(seats))
        spec = [("DOUBLE", "glass", f"+{per_hand:,}", acting and st.get("can_double", not split)),
                ("HIT", "gold", None, acting), ("STAND", "ivory", None, acting),
                ("SPLIT", "glass", f"+{per_hand:,}", acting and st.get("can_split", False))]
        for (x, w, h), (label, style, sub, enabled) in zip(slots, spec):
            button(c, x, lay.control_y, w, h, label, style, sub=sub, disabled=not enabled)

    c.img.convert("RGB").save(out_path)
    return out_path


# ---------------------------------------------------------------------------
# Menu and store — centre-anchored columns, mirroring SceneBootstrap
# ---------------------------------------------------------------------------

def backdrop(c, w, h, pool_y=0.0):
    c.img.alpha_composite(art("Table/room_base.png").resize((w, h), Image.LANCZOS))
    c.img.alpha_composite(art("Table/felt_base.png").resize((w, h), Image.LANCZOS))
    fiber = art("Table/felt_fiber.png")
    fl = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for yy in range(0, h, fiber.height):
        for xx in range(0, w, fiber.width):
            fl.alpha_composite(fiber, (xx, yy))
    c.img.alpha_composite(fl)
    c.img.alpha_composite(art("Table/vignette.png").resize((w, h), Image.LANCZOS))


def top_bar(c, W, t):
    c.img.alpha_composite(Image.new("RGBA", (c.img.width, int(100 + t)), HUD_BAND + (240,)), (0, 0))
    c.paste(tint(art("UI/ui_hairline.png").resize((900, 2), Image.LANCZOS), GOLD, 0.5), W / 2, 100)


def column_scale(W, H, design, reserved):
    return clamp(min((H - reserved) / design[1], (W - 32) / design[0]), 0.5, 1.0)


def render_menu(dev_w, dev_h, insets, out_path):
    l, t, r, b = insets
    W, H = dev_w - l - r, dev_h - t - b
    c = Canvas(dev_w, dev_h, l, t)
    backdrop(c, dev_w, dev_h)
    glow(c, W / 2, H / 2 - 120, 1500, 1400, (255, 236, 200), 0.10)
    top_bar(c, W, t)
    c.paste(tint(art("UI/icon_chip.png").resize((34, 34), Image.LANCZOS), GOLD, 1.0), 44, 50)
    draw_text(c, "12,450", 74, 50, F(SANS_SEMI, 34), IVORY, anchor="l")
    icon_button(c, "icon_plus", 262, 50, 44)
    for i, ic in enumerate(["icon_settings", "icon_stats", "icon_gift"]):
        icon_button(c, ic, W - 56 - i * 80, 50, 64, dot=(ic == "icon_gift"))

    k = column_scale(W, H, (700, 1000), 180)
    cx, cy = W / 2, H / 2 + 10

    def at(y):
        return cy - y * k

    c.paste(tint(art("UI/icon_spade.png").resize((int(64 * k), int(64 * k)), Image.LANCZOS), GOLD, 1.0), cx, at(400))
    draw_text(c, "BLACKJACK", cx, at(300), F(SERIF, 92 * k), GOLD, tracking=22 * k, gradient=((236, 214, 160), (186, 148, 80)))
    c.paste(tint(art("UI/ui_hairline.png").resize((int(420 * k), 2), Image.LANCZOS), GOLD, 0.6), cx, at(222))
    draw_text(c, "THE PRIVATE TABLE", cx, at(186), F(SANS_MED, 20 * k), MUTED, tracking=8 * k)
    button(c, cx, at(40), 560 * k, 120 * k, "PLAY", "gold", glow_on=True)
    button(c, cx, at(-104), 560 * k, 108 * k, "STORE", "glass")
    button(c, cx, at(-236), 560 * k, 108 * k, "DAILY REWARD", "glass", sub="READY TO CLAIM")
    draw_text(c, "Your daily reward is ready", cx, at(-340), F(SANS_MED, 24 * k), MUTED)
    draw_text(c, "Virtual chips only  \u00B7  no cash value", cx, H - 34, F(SANS_MED, 18), FAINT)
    c.img.convert("RGB").save(out_path)
    return out_path


PACKS = [("10,000", None, "$1.99", 0), ("60,000", "+5,000 BONUS", "$4.99", 1),
         ("175,000", "+25,000 BONUS", "$9.99", 2), ("625,000", "+125,000 BONUS", "$19.99", 3)]


def render_store(dev_w, dev_h, insets, out_path, success=False):
    l, t, r, b = insets
    W, H = dev_w - l - r, dev_h - t - b
    c = Canvas(dev_w, dev_h, l, t)
    backdrop(c, dev_w, dev_h)
    glow(c, W / 2, H / 2 - 60, 1400, 1500, (255, 236, 200), 0.10)
    top_bar(c, W, t)
    icon_button(c, "icon_back", 56, 50, 64)
    draw_text(c, "CHIPS", W / 2, 50, F(SERIF, 36), GOLD, tracking=12, gradient=((236, 214, 160), (186, 148, 80)))
    for i, ic in enumerate(["icon_settings", "icon_stats", "icon_gift"]):
        icon_button(c, ic, W - 56 - i * 80, 50, 64, dot=(ic == "icon_gift"))

    k = column_scale(W, H, (780, 1040), 120)
    cx, cy = W / 2, H / 2 + 20

    def at(y):
        return cy - y * k

    draw_text(c, "BALANCE", cx, at(468), F(SANS_MED, 18 * k), FAINT, tracking=4.5 * k)
    draw_text(c, "12,450", cx, at(424), F(SANS_SEMI, 44 * k), IVORY)
    for i, (amount, bonus, price, art_i) in enumerate(PACKS):
        ry = at(20 + 350 - 75 - i * 166)
        rw, rh = 760 * k, 150 * k
        c.paste(nine("UI/ui_shadow.b100.png", 100, rw + 80, rh + 80, (0, 0, 0), 0.5), cx, ry + 6 * k)
        c.paste(nine("UI/ui_rrect.b48.png", 48, rw, rh, (22, 26, 24), 0.92, radius=22 * k), cx, ry)
        c.paste(nine("UI/ui_rrect_stroke.b48.png", 48, rw, rh, GOLD, 0.28, radius=22 * k), cx, ry)
        pack = art(f"Chips/pack_{art_i}.png")
        ph = 118 * k
        c.paste(pack.resize((int(pack.width * ph / pack.height), int(ph)), Image.LANCZOS), cx - 284 * k, ry)
        draw_text(c, amount, cx - 160 * k, ry - 16 * k, F(SANS_SEMI, 44 * k), IVORY, anchor="l")
        if bonus:
            draw_text(c, bonus, cx - 160 * k, ry + 30 * k, F(SANS_MED, 18 * k), GOLD, tracking=3 * k, anchor="l")
        c.paste(nine("UI/ui_pill.b64.png", 64, 168 * k, 72 * k, GOLD, 1.0, radius=36 * k), cx + 264 * k, ry)
        draw_text(c, price, cx + 264 * k, ry, F(SANS_SEMI, 28 * k), INK)
        if i == 3:
            c.paste(nine("UI/ui_pill.b64.png", 64, 150 * k, 32 * k, GOLD, 1.0, radius=16 * k), cx - 284 * k, ry - 69 * k)
            draw_text(c, "BEST VALUE", cx - 284 * k, ry - 69 * k, F(SANS_MED, 15 * k), INK, tracking=3 * k)
    draw_text(c, "Chips are virtual and have no cash value.", cx, H - 34, F(SANS_MED, 18), FAINT)

    if success:
        c.img.alpha_composite(Image.new("RGBA", c.img.size, (0, 0, 0, 168)))
        fw, fh = 600, 500
        c.paste(nine("UI/ui_shadow.b100.png", 100, fw + 120, fh + 120, (0, 0, 0), 0.7), W / 2, H / 2)
        c.paste(nine("UI/ui_rrect.b48.png", 48, fw, fh, (22, 26, 24), 0.97, radius=28), W / 2, H / 2)
        c.paste(nine("UI/ui_rrect_stroke.b48.png", 48, fw, fh, GOLD, 0.32, radius=28), W / 2, H / 2)
        draw_text(c, "PURCHASE COMPLETE", W / 2, H / 2 - 180, F(SERIF, 36), IVORY, tracking=6)
        pk = art("Chips/pack_3.png").resize((240, 180), Image.LANCZOS)
        c.paste(pk, W / 2, H / 2 - 50)
        draw_text(c, "+60,000 CHIPS", W / 2, H / 2 + 76, F(SANS_SEMI, 44), GOLD, tracking=1)
        button(c, W / 2, H / 2 + 176, 300, 92, "COLLECT", "gold")
    c.img.convert("RGB").save(out_path)
    return out_path


DEVICES = {
    # Canvas units after CanvasScaler (ref 1080x1920, match 0.5); insets are (left, top, right, bottom).
    "phone": (979, 2119, (0, 118, 0, 85)),        # 19.5:9 phone: notch + home indicator
    "phone16x9": (1080, 1920, (0, 0, 0, 0)),
    "tablet": (1247, 1662, (0, 0, 0, 0)),          # 3:4 tablet, upright
    "fold": (1440, 1440, (0, 0, 0, 0)),            # square-ish foldable
    "desktop": (1920, 1080, (0, 0, 0, 0)),
    "tabletL": (1662, 1247, (0, 0, 0, 0)),
    "phoneL": (2119, 979, (118, 0, 118, 60)),      # phone on its side
    "ultrawide": (2200, 942, (0, 0, 0, 0)),        # 21:9 monitor
}


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "previews")
    os.makedirs(out, exist_ok=True)
    only_dev = os.environ.get("DEV")
    only_state = os.environ.get("STATE")
    for dev, (w, h, ins) in DEVICES.items():
        if only_dev and dev not in only_dev.split(","):
            continue
        for st in STATES:
            if only_state and st not in only_state.split(","):
                continue
            print(render(w, h, ins, st, os.path.join(out, f"{dev}_{st}.png")))
        if not only_state or "menu" in only_state.split(","):
            print(render_menu(w, h, ins, os.path.join(out, f"{dev}_menu.png")))
        if not only_state or "store" in only_state.split(","):
            print(render_store(w, h, ins, os.path.join(out, f"{dev}_store.png")))
            print(render_store(w, h, ins, os.path.join(out, f"{dev}_store_success.png"), success=True))
