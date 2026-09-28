"""
UI kit, table materials and line icons.

Kit sprites are drawn WHITE and tinted by the runtime (Image.color), so the palette
lives in code (Palette.cs) rather than being baked into dozens of PNGs. Nine-slice
borders are encoded in the file name (".b<N>"), which ArtImportSettings reads.

Table materials are deliberately featureless — gradients, fibre and light — so they
can be stretched to any aspect ratio. The table's physical features (rails, printed
lines, betting spot) are laid out live by the runtime, which is what lets the same
table work in portrait, landscape, phone and tablet.
"""

import math

from premium_common import (
    SS, Image, ImageDraw, ImageFilter, np, ramp, to_image, radial_t, smoothstep,
    seamless_noise, path, mirror_x, transform,
    FELT_LIT, FELT_MID, FELT_EDGE, CASINO_BLACK,
)

WHITE = (255, 255, 255, 255)


def _down(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------------------
# Kit
# ---------------------------------------------------------------------------

def rrect_fill(size=128, radius=48):
    big = Image.new("RGBA", (size * SS, size * SS), (255, 255, 255, 0))
    ImageDraw.Draw(big).rounded_rectangle((0, 0, size * SS - 1, size * SS - 1), radius=radius * SS, fill=WHITE)
    return _down(big, size, size)


def rrect_stroke(size=128, radius=48, width=3.0):
    big = Image.new("RGBA", (size * SS, size * SS), (255, 255, 255, 0))
    inset = width * SS / 2
    ImageDraw.Draw(big).rounded_rectangle(
        (inset, inset, size * SS - 1 - inset, size * SS - 1 - inset),
        radius=radius * SS - inset, outline=WHITE, width=int(width * SS))
    return _down(big, size, size)


def pill_fill(w=256, h=128):
    big = Image.new("RGBA", (w * SS, h * SS), (255, 255, 255, 0))
    ImageDraw.Draw(big).rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), radius=h * SS / 2, fill=WHITE)
    return _down(big, w, h)


def pill_stroke(w=256, h=128, width=3.0):
    big = Image.new("RGBA", (w * SS, h * SS), (255, 255, 255, 0))
    inset = width * SS / 2
    ImageDraw.Draw(big).rounded_rectangle(
        (inset, inset, w * SS - 1 - inset, h * SS - 1 - inset), radius=h * SS / 2 - inset,
        outline=WHITE, width=int(width * SS))
    return _down(big, w, h)


def circle_fill(size=256):
    big = Image.new("RGBA", (size * SS, size * SS), (255, 255, 255, 0))
    ImageDraw.Draw(big).ellipse((0, 0, size * SS - 1, size * SS - 1), fill=WHITE)
    return _down(big, size, size)


def circle_stroke(size=256, width=4.0):
    big = Image.new("RGBA", (size * SS, size * SS), (255, 255, 255, 0))
    inset = width * SS / 2
    ImageDraw.Draw(big).ellipse((inset, inset, size * SS - 1 - inset, size * SS - 1 - inset),
                                outline=WHITE, width=int(width * SS))
    return _down(big, size, size)


def glow(size=256, falloff=2.2):
    t = radial_t(size, size)
    arr = np.zeros((size, size, 4))
    arr[..., :3] = 255
    arr[..., 3] = np.clip(1 - t, 0, 1) ** falloff * 255
    return to_image(arr)


def soft_rrect_shadow(size=256, inset=64, radius=36, blur=18):
    """A blurred rounded rect. Nine-slice border = inset + radius keeps the blur intact."""
    img = Image.new("L", (size, size), 0)
    ImageDraw.Draw(img).rounded_rectangle((inset, inset, size - 1 - inset, size - 1 - inset), radius=radius, fill=255)
    img = img.filter(ImageFilter.GaussianBlur(blur))
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.putalpha(img)
    return out


def focus_ring(size=192, radius=56, width=4.0, halo=10):
    """Keyboard-focus ring: a crisp stroke with a soft halo just outside it."""
    n = size * SS
    ring = Image.new("L", (n, n), 0)
    pad = halo * SS
    ImageDraw.Draw(ring).rounded_rectangle((pad, pad, n - 1 - pad, n - 1 - pad), radius=radius * SS,
                                           outline=255, width=int(width * SS))
    halo_img = ring.filter(ImageFilter.GaussianBlur(halo * SS / 2.2)).point(lambda v: int(v * 0.55))
    combined = Image.fromarray(np.maximum(np.asarray(ring), np.asarray(halo_img)))
    out = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    out.putalpha(combined)
    return _down(out, size, size)


def gradient_v(h=256):
    """White, opaque at the top fading to clear at the bottom. Tinted for bands and sheens."""
    t = np.linspace(0, 1, h)[:, None] * np.ones((1, 8))
    arr = np.zeros((h, 8, 4))
    arr[..., :3] = 255
    arr[..., 3] = (1 - t) * 255
    return to_image(arr)


def hairline_fade(w=512):
    """A 1-unit rule that fades out at both ends — a divider that never looks cut off."""
    x = np.linspace(-1, 1, w)[None, :] * np.ones((8, 1))
    arr = np.zeros((8, w, 4))
    arr[..., :3] = 255
    arr[..., 3] = (1 - smoothstep(0.35, 1.0, np.abs(x))) * 255
    return to_image(arr)


def bet_spot(w=512, squash=0.56):
    """The betting circle printed on the felt, drawn at the table's viewing angle."""
    h = int(w * squash)
    n_w, n_h = w * SS, h * SS
    big = Image.new("RGBA", (n_w, n_h), (255, 255, 255, 0))
    d = ImageDraw.Draw(big)
    for inset, width, alpha in ((4, 3.2, 255), (22, 1.4, 170)):
        i = inset * SS
        d.ellipse((i, i * squash, n_w - 1 - i, n_h - 1 - i * squash), outline=(255, 255, 255, alpha), width=int(width * SS))
    return _down(big, w, h)


def card_shadow(w=240, h=320, inset=40, radius=22, blur=14):
    img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(img).rounded_rectangle((inset, inset, w - 1 - inset, h - 1 - inset), radius=radius, fill=255)
    img = img.filter(ImageFilter.GaussianBlur(blur))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.putalpha(img)
    return out


# ---------------------------------------------------------------------------
# Table materials
# ---------------------------------------------------------------------------

def felt_base(size=1024):
    """Emerald-black felt with the light pooled where the cards land."""
    t = radial_t(size, size, 0.5, 0.46, 0.62, 0.58)
    arr = ramp(t, [(0.0, FELT_LIT), (0.45, FELT_MID), (1.0, FELT_EDGE), (1.6, (3, 16, 12))])
    return to_image(arr, seed=11).convert("RGB")


def felt_fiber(size=256):
    """Tileable fibre: fine grain plus faint directional nap, as luminance in RGB."""
    fine = seamless_noise(size, 0.7, 3)
    nap = seamless_noise(size, 2.5, 5)
    lum = 128 + fine * 38 + nap * 22
    arr = np.zeros((size, size, 4))
    arr[..., 0] = arr[..., 1] = arr[..., 2] = lum
    arr[..., 3] = 30
    return to_image(arr, seed=13)


def vignette(size=512):
    t = radial_t(size, size, 0.5, 0.5, 0.72, 0.72)
    arr = np.zeros((size, size, 4))
    arr[..., 3] = smoothstep(0.45, 1.05, t) ** 1.3 * 235
    return to_image(arr, seed=17)


def light_pool(size=512):
    t = radial_t(size, size)
    arr = np.zeros((size, size, 4))
    arr[..., :3] = 255
    arr[..., 3] = (1 - smoothstep(0.0, 1.0, t)) ** 1.8 * 255
    return to_image(arr, seed=19)


def room_base(size=512):
    """The darkness around the table: charcoal-green at the centre, falling to black."""
    t = radial_t(size, size, 0.5, 0.45, 0.75, 0.7)
    arr = ramp(t, [(0.0, (26, 34, 30)), (0.6, (14, 18, 16)), (1.2, CASINO_BLACK)])
    return to_image(arr, seed=23).convert("RGB")


# ---------------------------------------------------------------------------
# Line icons (white, 128 px, ~9 px strokes, round caps)
# ---------------------------------------------------------------------------

ICON = 128
STROKE = 9.0


def _icon_canvas():
    return Image.new("RGBA", (ICON * SS, ICON * SS), (255, 255, 255, 0))


def _line(d, pts, width=STROKE):
    w = int(width * SS)
    pts = [(x * SS, y * SS) for x, y in pts]
    d.line(pts, fill=WHITE, width=w, joint="curve")
    r = w / 2
    for (x, y) in (pts[0], pts[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=WHITE)


def _finish(img):
    return _down(img, ICON, ICON)


def icon_back():
    img = _icon_canvas()
    _line(ImageDraw.Draw(img), [(76, 30), (42, 64), (76, 98)])
    return _finish(img)


def icon_plus():
    img = _icon_canvas()
    d = ImageDraw.Draw(img)
    _line(d, [(64, 32), (64, 96)])
    _line(d, [(32, 64), (96, 64)])
    return _finish(img)


def icon_close():
    img = _icon_canvas()
    d = ImageDraw.Draw(img)
    _line(d, [(38, 38), (90, 90)])
    _line(d, [(90, 38), (38, 90)])
    return _finish(img)


def icon_gift():
    img = _icon_canvas()
    d = ImageDraw.Draw(img)
    w = int(STROKE * SS)
    d.rounded_rectangle((26 * SS, 56 * SS, 102 * SS, 104 * SS), radius=6 * SS, outline=WHITE, width=w)
    d.rounded_rectangle((20 * SS, 40 * SS, 108 * SS, 58 * SS), radius=6 * SS, outline=WHITE, width=w)
    _line(d, [(64, 40), (64, 104)])
    # Bow: two loops meeting at the knot.
    d.arc((34 * SS, 18 * SS, 66 * SS, 46 * SS), 150, 360 + 20, fill=WHITE, width=w)
    d.arc((62 * SS, 18 * SS, 94 * SS, 46 * SS), 160, 360 + 30, fill=WHITE, width=w)
    return _finish(img)


def icon_stats():
    img = _icon_canvas()
    d = ImageDraw.Draw(img)
    _line(d, [(36, 98), (36, 70)])
    _line(d, [(64, 98), (64, 34)])
    _line(d, [(92, 98), (92, 54)])
    return _finish(img)


def icon_settings():
    """A gear drawn as an outline: the silhouette minus its own erosion, plus the hub."""
    n = ICON * SS
    sil = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(sil)
    c = n / 2
    ro = 36 * SS
    d.ellipse((c - ro, c - ro, c + ro, c + ro), fill=255)
    for i in range(8):
        a = i * math.pi / 4
        tooth = [(-0.20, -1.30), (0.20, -1.30), (0.30, -0.85), (-0.30, -0.85)]
        d.polygon(transform(tooth, c, c, 36 * SS, a), fill=255)
    k = int(STROKE * SS) * 2 + 1
    inner = sil.filter(ImageFilter.MinFilter(k if k % 2 else k + 1))
    outline = np.clip(np.asarray(sil, dtype=np.int16) - np.asarray(inner, dtype=np.int16), 0, 255).astype(np.uint8)
    hub = Image.new("L", (n, n), 0)
    hr = 13 * SS
    w = int(STROKE * SS)
    ImageDraw.Draw(hub).ellipse((c - hr - w / 2, c - hr - w / 2, c + hr + w / 2, c + hr + w / 2), outline=255, width=w)
    mask = Image.fromarray(np.maximum(outline, np.asarray(hub)))
    out = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    out.putalpha(mask)
    return _finish(out)


def icon_chip():
    n = ICON * SS
    img = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    c = n / 2
    r = 46 * SS
    w = int(STROKE * SS)
    d.ellipse((c - r, c - r, c + r, c + r), outline=WHITE, width=w)
    r2 = 22 * SS
    d.ellipse((c - r2, c - r2, c + r2, c + r2), outline=WHITE, width=int(w * 0.7))
    for i in range(6):
        a = i * math.pi / 3 + math.pi / 6
        x0, y0 = c + math.cos(a) * (r - w * 0.5), c + math.sin(a) * (r - w * 0.5)
        x1, y1 = c + math.cos(a) * (r2 + w * 0.6), c + math.sin(a) * (r2 + w * 0.6)
        d.line((x0, y0, x1, y1), fill=WHITE, width=int(w * 0.7))
    return _finish(img)


def icon_undo():
    img = _icon_canvas()
    d = ImageDraw.Draw(img)
    w = int(STROKE * SS)
    d.arc((34 * SS, 34 * SS, 98 * SS, 98 * SS), 200, 360 + 150, fill=WHITE, width=w)
    _line(d, [(30, 44), (36, 66), (58, 60)])
    return _finish(img)


def icon_spade():
    """Filled spade for the brand mark."""
    from premium_common import draw_suit
    img = _icon_canvas()
    draw_suit(ImageDraw.Draw(img), "Spades", ICON * SS / 2, ICON * SS / 2 + 2 * SS, 100 * SS, WHITE)
    return _finish(img)


ICONS = {
    "icon_back": icon_back,
    "icon_plus": icon_plus,
    "icon_close": icon_close,
    "icon_gift": icon_gift,
    "icon_stats": icon_stats,
    "icon_settings": icon_settings,
    "icon_chip": icon_chip,
    "icon_undo": icon_undo,
    "icon_spade": icon_spade,
}
