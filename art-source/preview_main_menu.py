#!/usr/bin/env python3
"""
Composites the UI kit into a mock main menu at the game's reference resolution.

This is a design reference, not a build step — it lets the layout be judged without
a Unity round-trip, and it documents the intended geometry that SceneBootstrap
reproduces with real UI components.

    python3 art-source/preview_main_menu.py   ->  art-source/preview_main_menu.png
"""

import os

from PIL import Image, ImageDraw

import generate_ui_kit as kit

HERE = os.path.dirname(os.path.abspath(__file__))
UI = os.path.join(os.path.dirname(HERE), "Assets", "Art", "UI")

W, H = 1080, 1920


def load(name):
    return Image.open(os.path.join(UI, name + ".png")).convert("RGBA")


def nine_slice(img, border, w, h):
    """Scales a sprite the way Unity's Sliced image type does."""
    b = border
    sw, sh = img.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    parts = {
        "tl": (0, 0, b, b), "tm": (b, 0, sw - b, b), "tr": (sw - b, 0, sw, b),
        "ml": (0, b, b, sh - b), "mm": (b, b, sw - b, sh - b), "mr": (sw - b, b, sw, sh - b),
        "bl": (0, sh - b, b, sh), "bm": (b, sh - b, sw - b, sh), "br": (sw - b, sh - b, sw, sh),
    }
    c = {k: img.crop(v) for k, v in parts.items()}
    mw, mh = max(1, w - 2 * b), max(1, h - 2 * b)
    out.paste(c["mm"].resize((mw, mh), Image.BILINEAR), (b, b))
    out.paste(c["tm"].resize((mw, b), Image.BILINEAR), (b, 0))
    out.paste(c["bm"].resize((mw, b), Image.BILINEAR), (b, h - b))
    out.paste(c["ml"].resize((b, mh), Image.BILINEAR), (0, b))
    out.paste(c["mr"].resize((b, mh), Image.BILINEAR), (w - b, b))
    out.paste(c["tl"], (0, 0)); out.paste(c["tr"], (w - b, 0))
    out.paste(c["bl"], (0, h - b)); out.paste(c["br"], (w - b, h - b))
    return out


def fit(img, h):
    return img.resize((int(img.width * h / img.height), h), Image.LANCZOS)


def centre(dst, src, cx, cy):
    dst.alpha_composite(src, (int(cx - src.width / 2), int(cy - src.height / 2)))


def row(canvas, y, frame_name, icon_name, title, subtitle):
    x0, x1, h = 96, 984, 176
    canvas.alpha_composite(nine_slice(load(frame_name), 56, x1 - x0, h), (x0, y))

    centre(canvas, fit(load(icon_name), 76), x0 + 96, y + h / 2)
    centre(canvas, fit(load("icon_chevron"), 54), x1 - 78, y + h / 2)

    t = kit.gold_text(title, kit.font(kit.DISPLAY_FONT, 62), tracking=3, stroke=4, flat=True)
    centre(canvas, t, (x0 + x1) / 2 + 20, y + h * 0.40)

    s = kit.font(kit.BODY_FONT, 30)
    d = ImageDraw.Draw(canvas)
    bb = d.textbbox((0, 0), subtitle, font=s)
    d.text(((x0 + x1) / 2 + 20 - (bb[2] - bb[0]) / 2, y + h * 0.63), subtitle,
           font=s, fill=(226, 208, 160, 235))


def main():
    canvas = load("felt_menu").copy()

    # ---- top bar -------------------------------------------------------
    pill_w, pill_h = 400, 104
    canvas.alpha_composite(nine_slice(load("pill.b58"), 58, pill_w, pill_h), (40, 52))
    centre(canvas, fit(load("icon_coin"), 78), 40 + 56, 52 + pill_h / 2)
    centre(canvas, fit(load("icon_plus"), 52), 40 + pill_w - 54, 52 + pill_h / 2)
    bal = kit.gold_text("5,000", kit.font(kit.DISPLAY_FONT, 56), stroke=3, flat=True)
    centre(canvas, bal, 40 + pill_w / 2 + 6, 52 + pill_h / 2)

    for i, ic in enumerate(("icon_gift", "icon_settings", "icon_trophy")):
        cx = 762 + i * 128
        centre(canvas, fit(load("circle_button"), 104), cx, 52 + pill_h / 2)
        centre(canvas, fit(load(ic), 56), cx, 52 + pill_h / 2)

    # ---- crest + wordmark ----------------------------------------------
    centre(canvas, fit(load("emblem_ace"), 430), W / 2, 400)
    title = kit.gold_text("BLACKJACK", kit.font(kit.DISPLAY_FONT, 210), tracking=6, stroke=9)
    centre(canvas, fit(title, int(title.height * 880 / title.width)), W / 2, 690)

    centre(canvas, fit(load("divider"), 40), W / 2, 772)
    tag = kit.gold_text("BEAT THE DEALER.  WIN BIG.",
                        kit.font(kit.BODY_FONT, 36), tracking=6, stroke=2, flat=True)
    centre(canvas, tag, W / 2, 812)

    # ---- action rows ----------------------------------------------------
    row(canvas, 900, "btn_green.b56", "icon_cards", "PLAY", "DEAL YOUR LUCK")
    row(canvas, 1110, "btn_blue.b56", "icon_cart", "STORE", "GET CHIPS & ITEMS")
    row(canvas, 1320, "btn_red.b56", "icon_gift", "DAILY REWARDS", "CLAIM YOUR PRIZE")

    # ---- house rules ----------------------------------------------------
    centre(canvas, fit(load("divider"), 36), W / 2, 1600)
    r1 = kit.gold_text("BLACKJACK PAYS 3 TO 2",
                       kit.font(kit.DISPLAY_FONT, 46), tracking=3, stroke=3, flat=True)
    centre(canvas, r1, W / 2, 1660)
    r2 = kit.gold_text("DEALER MUST HIT SOFT 17",
                       kit.font(kit.BODY_FONT, 30), tracking=4, stroke=2, flat=True)
    centre(canvas, r2, W / 2, 1716)
    centre(canvas, fit(load("divider"), 36), W / 2, 1772)

    out = os.path.join(HERE, "preview_main_menu.png")
    canvas.convert("RGB").save(out)
    print("wrote", out)


if __name__ == "__main__":
    main()
