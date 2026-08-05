#!/usr/bin/env python3
"""
Composites the extracted table art and UI kit into a mock game screen at the game's
reference resolution, using the same geometry SceneBootstrap bakes into the scene.

This is a design reference, not a build step — it lets the in-round layout (top bar,
dealer/player hands, felt stake, action band) be judged without a Unity round-trip.

    python3 art-source/preview_game_table.py  ->  art-source/preview_game_table.png
"""

import os

from PIL import Image

import generate_ui_kit as kit
from preview_main_menu import nine_slice, fit, centre  # same compositing helpers

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(os.path.dirname(HERE), "Assets", "Art")

W, H = 1080, 1920
SCALE = 1080 / 1024.0


def load(*parts):
    return Image.open(os.path.join(ART, *parts)).convert("RGBA")


def ui(name):
    return load("UI", name + ".png")


# SceneBootstrap lays out in canvas-centre space (x right, y up); PIL wants top-left.
def px(x):
    return 540 + x


def py(y):
    return 960 - y


def src_y(y):
    """Same mapping SceneBootstrap.SrcY uses: render-space y -> canvas-centre y."""
    return 960 - (110 + y * SCALE)


def card(canvas, name, cx, cy, w, h):
    shadow = nine_slice(ui("card_shadow.b64"), 64, int(w + 30), int(h + 30))
    centre(canvas, shadow, px(cx) + 5, py(cy) + 9)
    face = load("Cards", name + ".png").resize((int(w), int(h)), Image.LANCZOS)
    centre(canvas, face, px(cx), py(cy))


def hand(canvas, names, cx, cy, w, h, step):
    x0 = cx - step * (len(names) - 1) / 2
    for i, name in enumerate(names):
        card(canvas, name, x0 + i * step, cy, w, h)


def circle_button(canvas, icon, cx, cy, d):
    centre(canvas, fit(ui("circle_button"), int(d)), px(cx), py(cy))
    centre(canvas, fit(ui(icon), int(d * 0.54)), px(cx), py(cy))


def action_button(canvas, frame, icon, label, cx, cy, w, h, label_size):
    centre(canvas, nine_slice(ui(frame + ".b46"), 46, int(w), int(h)), px(cx), py(cy))
    if icon:
        centre(canvas, fit(ui(icon), int(h * 0.34)), px(cx), py(cy) - int(h * 0.17))
    t = kit.gold_text(label, kit.font(kit.DISPLAY_FONT, label_size),
                      tracking=2, stroke=3, flat=True)
    ty = py(cy) + (int(h * 0.24) if icon else 0)
    centre(canvas, t, px(cx), ty)


def chip(canvas, label, cx, cy, d, label_size):
    art = ui("chip_stack")
    art = art.resize((int(d), int(d * art.height / art.width)), Image.LANCZOS)
    centre(canvas, art, px(cx), py(cy))
    t = kit.gold_text(label, kit.font(kit.DISPLAY_FONT, label_size),
                      tracking=1, stroke=3, flat=True)
    centre(canvas, t, px(cx), py(cy) - int(d * 0.02))


def gold_line(canvas, text, size, cx, cy, tracking=2, stroke=3):
    t = kit.gold_text(text, kit.font(kit.DISPLAY_FONT, size),
                      tracking=tracking, stroke=stroke, flat=True)
    centre(canvas, t, px(cx), py(cy))


def layout_slots(widths, usable=960.0, margin=16.0):
    """Mirror of SceneBootstrap.LayoutSlots — one spacing rule for the band."""
    gutter = (usable - 2 * margin - sum(widths)) / (len(widths) - 1)
    slots, cursor = [], -usable / 2 + margin
    for w in widths:
        slots.append(cursor + w / 2)
        cursor += w + gutter
    return slots


def main():
    canvas = load("Table", "TableBackground.png").convert("RGBA")

    # ---- top bar (y = 864 in centre space) ------------------------------
    bar_y = 864
    circle_button(canvas, "icon_back", -430, bar_y, 92)
    centre(canvas, nine_slice(ui("pill.b58"), 58, 330, 96), px(-176), py(bar_y))
    centre(canvas, fit(ui("icon_coin"), 68), px(-302), py(bar_y))
    gold_line(canvas, "5,000", 44, -186, bar_y, tracking=1)
    circle_button(canvas, "icon_plus", -42, bar_y, 64)
    circle_button(canvas, "icon_gift", 230, bar_y, 92)
    # Daily-reward dot pinned to the gift button's shoulder.
    dot = fit(ui("circle_button"), 33).copy()
    gold = Image.new("RGBA", dot.size, (255, 184, 64, 255))
    dot = Image.composite(gold, dot, dot.split()[3])
    centre(canvas, dot, px(230 + 31), py(bar_y + 31))
    circle_button(canvas, "icon_settings", 332, bar_y, 92)
    circle_button(canvas, "icon_trophy", 434, bar_y, 92)

    # ---- dealer ---------------------------------------------------------
    hand(canvas, ["card_Hearts_10", "card_Back"], 4, src_y(300), 172, 241, 106)
    gold_line(canvas, "DEALER  \u2022  10", 38, 2, src_y(150), tracking=3)

    # ---- player ---------------------------------------------------------
    hand(canvas, ["card_Spades_13", "card_Diamonds_10"], -44, src_y(828), 206, 288, 122)
    gold_line(canvas, "YOU  \u2022  20", 38, 0, src_y(658), tracking=3)
    chip(canvas, "500", 405, src_y(884), 150, 38)

    # ---- action band (bottom strip) -------------------------------------
    band_y = src_y(1179)
    slots = layout_slots([188, 246, 246, 188])
    action_button(canvas, "btn_blue", "icon_double", "DOUBLE", slots[0], band_y, 188, 164, 30)
    action_button(canvas, "btn_green", "icon_hit", "HIT", slots[1], band_y, 246, 200, 44)
    action_button(canvas, "btn_red", "icon_stand", "STAND", slots[2], band_y, 246, 200, 40)
    action_button(canvas, "btn_dark", "icon_split", "SPLIT", slots[3], band_y, 188, 164, 30)

    out = os.path.join(HERE, "preview_game_table.png")
    canvas.convert("RGB").save(out)
    print("wrote", out)

    # ---- second frame: betting phase ------------------------------------
    canvas = load("Table", "TableBackground.png").convert("RGBA")
    circle_button(canvas, "icon_back", -430, bar_y, 92)
    centre(canvas, nine_slice(ui("pill.b58"), 58, 330, 96), px(-176), py(bar_y))
    centre(canvas, fit(ui("icon_coin"), 68), px(-302), py(bar_y))
    gold_line(canvas, "4,500", 44, -186, bar_y, tracking=1)
    circle_button(canvas, "icon_plus", -42, bar_y, 64)
    circle_button(canvas, "icon_gift", 230, bar_y, 92)
    circle_button(canvas, "icon_settings", 332, bar_y, 92)
    circle_button(canvas, "icon_trophy", 434, bar_y, 92)

    gold_line(canvas, "DEALER", 38, 2, src_y(150), tracking=3)
    gold_line(canvas, "TAP A CHIP TO PLACE YOUR BET", 34, 0, src_y(658), tracking=3)
    chip(canvas, "500", 405, src_y(884), 150, 38)

    chips = layout_slots([106, 106, 106, 106, 330])
    for slot, label in zip(chips[:4], ("100", "500", "1K", "5K")):
        chip(canvas, label, slot, band_y, 106, 27)
    action_button(canvas, "btn_green", None, "DEAL", chips[4], band_y, 330, 170, 56)

    out = os.path.join(HERE, "preview_game_bet.png")
    canvas.convert("RGB").save(out)
    print("wrote", out)


if __name__ == "__main__":
    main()
