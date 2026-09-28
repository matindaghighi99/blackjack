"""
Casino chips, one colourway per denomination.

Two renders per value:
  chip_<v>.png        top-down, for the chip rack the player taps
  chip_<v>_side.png   seen at the table's viewing angle, with visible edge thickness,
                      so stacking N of them with a small vertical step reads as a
                      physical stack on the felt

The value itself is NOT baked in — the runtime draws it with TextMeshPro, so a
config change to the denominations never shows a wrong number.

Colours follow casino convention (5 red, 25 green, 100 black, 500 purple, 1K ochre)
but are deliberately desaturated so a rack of them reads as one family.
"""

import math

from premium_common import (
    SS, Image, ImageDraw, ImageFilter, np, ramp, to_image, radial_t, smoothstep,
    IVORY, GOLD, GOLD_DEEP,
)

ONYX = (27, 28, 31)

# value: (body, inserts, inlay ring)
COLOURWAYS = {
    1: ((226, 219, 204), (74, 85, 96), GOLD_DEEP),
    5: ((122, 30, 39), IVORY, GOLD),
    10: ((44, 74, 107), IVORY, GOLD),
    25: ((29, 90, 69), IVORY, GOLD),
    50: ((140, 74, 36), IVORY, GOLD),
    100: (ONYX, GOLD, GOLD),
    250: ((142, 74, 94), IVORY, GOLD),
    500: ((78, 45, 92), IVORY, GOLD),
    1000: ((176, 132, 50), ONYX, ONYX),
    2500: ((34, 96, 104), IVORY, GOLD),
    5000: ((94, 70, 54), IVORY, GOLD),
    10000: ((144, 151, 160), ONYX, ONYX),
}

SIZE = 256
R = 118  # chip radius inside the 256 canvas; the margin keeps the AA edge clean
INSERTS = 8
INSERT_SPAN = math.radians(15)


def _shade(c, k):
    if k >= 1:
        return tuple(min(255, int(v + (255 - v) * (k - 1))) for v in c)
    return tuple(int(v * k) for v in c)


def chip_face(value):
    body, insert, ring = COLOURWAYS[value]
    n = SIZE * SS
    c = n / 2
    r = R * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Body.
    d.ellipse((c - r, c - r, c + r, c + r), fill=body + (255,))

    # Edge inserts: rectangular spots around the rim, as on a clay casino chip.
    outer, inner = r * 0.995, r * 0.77
    for i in range(INSERTS):
        a0 = i * 2 * math.pi / INSERTS - INSERT_SPAN / 2
        a1 = a0 + INSERT_SPAN
        pts = []
        for k in range(12):
            a = a0 + (a1 - a0) * k / 11
            pts.append((c + math.cos(a) * outer, c + math.sin(a) * outer))
        for k in range(12):
            a = a1 - (a1 - a0) * k / 11
            pts.append((c + math.cos(a) * inner, c + math.sin(a) * inner))
        d.polygon(pts, fill=insert + (255,))

    # Fine ring of dashes just inside the inserts.
    dash_r = r * 0.715
    for i in range(48):
        a = i * 2 * math.pi / 48
        if i % 6 == 0:
            continue  # breathing room opposite the inserts
        x0, y0 = c + math.cos(a) * dash_r, c + math.sin(a) * dash_r
        x1, y1 = c + math.cos(a + 0.05) * dash_r, c + math.sin(a + 0.05) * dash_r
        d.line((x0, y0, x1, y1), fill=insert + (150,), width=int(1.6 * SS))

    # Centre inlay where the live value sits.
    ir = r * 0.58
    inlay = _shade(body, 1.10) if body != ONYX else (38, 39, 43)
    d.ellipse((c - ir, c - ir, c + ir, c + ir), fill=inlay + (255,), outline=ring + (255,), width=int(2.2 * SS))
    ir2 = ir - 6 * SS
    d.ellipse((c - ir2, c - ir2, c + ir2, c + ir2), outline=ring + (110,), width=int(1.0 * SS))

    img = img.resize((SIZE, SIZE), Image.LANCZOS)

    # Lighting: a soft top-left key light and a darkened rim for a bevelled edge.
    t = radial_t(SIZE, SIZE, 0.5, 0.5, R / SIZE, R / SIZE)
    light = radial_t(SIZE, SIZE, 0.36, 0.30, 0.9, 0.9)
    shade = np.zeros((SIZE, SIZE, 4))
    rim = smoothstep(0.86, 1.0, t)
    shade[..., 3] = rim * 70
    shade_img = to_image(shade)
    hi = np.zeros((SIZE, SIZE, 4))
    hi[..., :3] = 255
    hi[..., 3] = np.clip(1.0 - light, 0, 1) ** 2.2 * 34
    hi_img = to_image(hi)

    alpha = img.split()[3]
    for layer in (shade_img, hi_img):
        layer.putalpha(Image.fromarray(
            (np.asarray(layer.split()[3], dtype=np.float64) * np.asarray(alpha, dtype=np.float64) / 255).astype(np.uint8)))
        img.alpha_composite(layer)
    return img


SIDE_SQUASH = 0.56   # vertical scale of the face at the table's viewing angle
EDGE = 20            # chip thickness in pixels at this render size


def chip_side(value):
    """The chip at the table's viewing angle: squashed face on top of a visible edge."""
    body, insert, _ = COLOURWAYS[value]
    face = chip_face(value)
    fh = int(SIZE * SIDE_SQUASH)
    face = face.resize((SIZE, fh), Image.LANCZOS)

    h = fh + EDGE
    n_w, n_h = SIZE * SS, h * SS
    edge = Image.new("RGBA", (n_w, n_h), (0, 0, 0, 0))
    d = ImageDraw.Draw(edge)
    cx = n_w / 2
    rx = R * SS
    ry = R * SIDE_SQUASH * SS
    cy = fh / 2 * SS
    t = EDGE * SS

    # The edge band is the lower half-ellipse swept down by the thickness.
    dark = _shade(body, 0.62)
    d.ellipse((cx - rx, cy - ry + t, cx + rx, cy + ry + t), fill=dark + (255,))
    d.rectangle((cx - rx, cy, cx + rx, cy + t), fill=dark + (255,))

    # Inserts continue down the edge where they fall on the visible (front) half.
    for i in range(INSERTS):
        a0 = i * 2 * math.pi / INSERTS - INSERT_SPAN / 2
        a1 = a0 + INSERT_SPAN
        if math.sin((a0 + a1) / 2) <= 0.05:
            continue
        top = [(cx + math.cos(a0 + (a1 - a0) * k / 10) * rx, cy + math.sin(a0 + (a1 - a0) * k / 10) * ry)
               for k in range(11)]
        bottom = [(x, y + t) for (x, y) in reversed(top)]
        d.polygon(top + bottom, fill=_shade(insert, 0.8) + (255,))

    edge = edge.resize((SIZE, h), Image.LANCZOS)

    # Vertical shading across the edge so it reads as a cylinder, lit from above.
    arr = np.asarray(edge, dtype=np.float64).copy()
    xs = (np.arange(SIZE) + 0.5 - SIZE / 2) / R
    curve = np.clip(1 - xs ** 2, 0, 1) ** 0.5
    arr[..., :3] *= (0.72 + 0.34 * curve)[None, :, None]
    edge = to_image(arr)

    edge.alpha_composite(face, (0, 0))
    return edge


def chip_shadow():
    """Soft contact shadow placed under a chip stack on the felt."""
    w, h = 256, 128
    t = radial_t(w, h, 0.5, 0.5, 0.46, 0.42)
    arr = np.zeros((h, w, 4))
    arr[..., 3] = (1 - smoothstep(0.35, 1.0, t)) * 150
    return to_image(arr)
