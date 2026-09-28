"""
Shared drawing primitives for the premium art pipeline.

Everything is drawn as vector geometry at SS x supersampling and filtered down with
Lanczos, so edges are smooth at the target size without any hand-painted pixels. The
palette here is the single source of truth for the art; the runtime mirrors it in
Assets/Scripts/UI/Theme/Palette.cs.
"""

import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ART = os.path.join(ROOT, "Assets", "Art")
FONTS = os.path.join(ROOT, "Assets", "Fonts")

SS = 4  # supersampling factor

# ---------------------------------------------------------------------------
# Palette (sRGB)
# ---------------------------------------------------------------------------

CASINO_BLACK = (11, 13, 12)
CHARCOAL = (22, 26, 24)
GOLD = (201, 164, 92)
GOLD_LIGHT = (231, 207, 150)
GOLD_DEEP = (132, 99, 45)
IVORY = (243, 235, 221)

FELT_LIT = (22, 86, 63)
FELT_MID = (12, 58, 42)
FELT_EDGE = (5, 28, 20)

INK_RED = (176, 32, 44)
INK_BLACK = (22, 23, 27)

STOCK_TOP = (253, 251, 246)
STOCK_BOTTOM = (244, 240, 230)
STOCK_EDGE = (206, 199, 184)

# Fonts used only to bake art. Serif display for card courts and indices; the
# runtime uses the same families via TextMeshPro.
SERIF = os.path.join(FONTS, "EBGaramond-Regular-Lining.otf")
SERIF_BOLD_SYSTEM = "/usr/share/fonts/opentype/ebgaramond/EBGaramond12-Bold.otf"
SANS_SEMIBOLD = os.path.join(FONTS, "Inter-SemiBold-Tabular.otf")


def font(path, size):
    return ImageFont.truetype(path, int(size))


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)
    return path


# ---------------------------------------------------------------------------
# Numeric helpers
# ---------------------------------------------------------------------------

def ramp(t, stops):
    """Maps an array t through colour stops [(pos, (r,g,b[,a])), ...] -> float RGBA array."""
    positions = [s[0] for s in stops]
    cols = [tuple(s[1]) + ((255,) if len(s[1]) == 3 else ()) for s in stops]
    out = np.empty(t.shape + (4,), dtype=np.float64)
    for ch in range(4):
        out[..., ch] = np.interp(t, positions, [c[ch] for c in cols])
    return out


def to_image(arr, dither=True, seed=7):
    """Float RGBA array -> uint8 image, with sub-LSB dither so big gradients never band."""
    if dither:
        rng = np.random.default_rng(seed)
        arr = arr + rng.uniform(-0.5, 0.5, arr.shape)
    return Image.fromarray(np.clip(np.round(arr), 0, 255).astype(np.uint8), "RGBA")


def radial_t(w, h, cx=0.5, cy=0.5, rx=0.5, ry=0.5):
    y, x = np.mgrid[0:h, 0:w]
    dx = ((x + 0.5) / w - cx) / rx
    dy = ((y + 0.5) / h - cy) / ry
    return np.sqrt(dx * dx + dy * dy)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def seamless_noise(size, sigma, seed):
    """Blurred white noise that tiles perfectly (blur done in the frequency domain)."""
    rng = np.random.default_rng(seed)
    n = rng.standard_normal((size, size))
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    kernel = np.exp(-2 * (math.pi ** 2) * (sigma ** 2) * (fx ** 2 + fy ** 2))
    out = np.real(np.fft.ifft2(np.fft.fft2(n) * kernel))
    out -= out.mean()
    out /= (out.std() + 1e-9)
    return out


# ---------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------

def bezier(p0, p1, p2, p3, n=40):
    pts = []
    for i in range(n + 1):
        t = i / n
        mt = 1 - t
        x = mt ** 3 * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t ** 3 * p3[0]
        y = mt ** 3 * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t ** 3 * p3[1]
        pts.append((x, y))
    return pts


def path(start, segments, n=40):
    """start point + list of (c1, c2, end) cubic segments -> polyline."""
    pts = [start]
    cur = start
    for c1, c2, end in segments:
        pts.extend(bezier(cur, c1, c2, end, n)[1:])
        cur = end
    return pts


def mirror_x(half):
    """Right-half outline (top to bottom or bottom to top) -> closed symmetric outline."""
    left = [(-x, y) for (x, y) in reversed(half)]
    return half + left[1:]


def transform(pts, cx, cy, s, rot=0.0):
    c, sn = math.cos(rot), math.sin(rot)
    return [(cx + (x * c - y * sn) * s, cy + (x * sn + y * c) * s) for (x, y) in pts]


def rounded_rect_mask(w, h, r, ss=SS):
    """Anti-aliased rounded-rect mask at final size w x h."""
    big = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(big).rounded_rectangle((0, 0, w * ss - 1, h * ss - 1), radius=r * ss, fill=255)
    return big.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------------------
# Suit outlines, unit scale (roughly within [-1, 1]), y pointing down
# ---------------------------------------------------------------------------

def heart_outline():
    right = path((0.0, 0.98), [
        ((0.30, 0.64), (1.0, 0.22), (1.0, -0.33)),
        ((1.0, -0.70), (0.76, -0.92), (0.50, -0.92)),
        ((0.25, -0.92), (0.04, -0.74), (0.0, -0.50)),
    ])
    return mirror_x(right)


def spade_body():
    right = path((0.0, -1.0), [
        ((0.26, -0.62), (1.0, -0.30), (1.0, 0.22)),
        ((1.0, 0.56), (0.76, 0.74), (0.50, 0.74)),
        ((0.28, 0.74), (0.10, 0.60), (0.03, 0.44)),
    ])
    return mirror_x(right)


def stem():
    # A slim stem that flares into a curved foot.
    right = path((0.04, 0.30), [
        ((0.07, 0.62), (0.18, 0.88), (0.36, 0.98)),
        ((0.30, 1.0), (0.10, 1.0), (0.0, 1.0)),
    ])
    return mirror_x(right)


def diamond_outline(bow=0.035):
    corners = [(0.0, -1.0), (0.74, 0.0), (0.0, 1.0), (-0.74, 0.0)]
    pts = []
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        # Pull each side's midpoint toward the centre: the faintly concave sides of a
        # printed diamond.
        c1 = (a[0] + (b[0] - a[0]) * 0.33 - mx * bow, a[1] + (b[1] - a[1]) * 0.33 - my * bow)
        c2 = (a[0] + (b[0] - a[0]) * 0.66 - mx * bow, a[1] + (b[1] - a[1]) * 0.66 - my * bow)
        pts.extend(bezier(a, c1, c2, b, 30)[:-1])
    return pts


def draw_suit(draw, suit, cx, cy, size, fill, rot=0.0):
    """Draws a suit symbol whose height is roughly `size` pixels."""
    s = size / 2.0
    if suit == "Hearts":
        draw.polygon(transform(heart_outline(), cx, cy, s, rot), fill=fill)
    elif suit == "Diamonds":
        draw.polygon(transform(diamond_outline(), cx, cy, s, rot), fill=fill)
    elif suit == "Spades":
        draw.polygon(transform(spade_body(), cx, cy - s * 0.08, s * 0.94, rot), fill=fill)
        draw.polygon(transform(stem(), cx, cy - s * 0.08, s * 0.94, rot), fill=fill)
    elif suit == "Clubs":
        k = s * 0.94
        lobes = ((0.0, -0.52), (-0.48, 0.10), (0.48, 0.10))
        r = 0.40 * k
        for (x, y) in lobes:
            px, py = _rot(x, y, rot)
            draw.ellipse((cx + px * k - r, cy + py * k - r, cx + px * k + r, cy + py * k + r), fill=fill)
        # Fill the space between the three lobe centres so the leaf is one mass.
        draw.polygon(transform(list(lobes), cx, cy, k, rot), fill=fill)
        draw.polygon(transform(club_stem(), cx, cy, k, rot), fill=fill)


def club_stem():
    # Starts inside the lobe mass so there is no visible ledge where it joins.
    right = path((0.05, 0.05), [
        ((0.07, 0.55), (0.16, 0.86), (0.38, 0.98)),
        ((0.30, 1.0), (0.10, 1.0), (0.0, 1.0)),
    ])
    return mirror_x(right)


def _rot(x, y, rot):
    c, s = math.cos(rot), math.sin(rot)
    return x * c - y * s, x * s + y * c


def soft_shadow(mask, blur, opacity, color=(0, 0, 0)):
    """Blurred, tinted copy of an L mask, as an RGBA layer."""
    m = mask.filter(ImageFilter.GaussianBlur(blur))
    layer = Image.new("RGBA", mask.size, color + (0,))
    layer.putalpha(m.point(lambda v: int(v * opacity)))
    return layer
