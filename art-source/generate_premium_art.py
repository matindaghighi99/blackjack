#!/usr/bin/env python3
"""
Generates every sprite the premium table uses.

    python3 art-source/prepare_fonts.py          # once, fonts first (cards bake type)
    python3 art-source/generate_premium_art.py

Outputs
    Assets/Art/Cards/card_<Suit>_<01..13>.png, card_Back.png
    Assets/Art/Chips/chip_<value>.png, chip_<value>_side.png, chip_shadow.png, pack_<0..3>.png
    Assets/Art/Table/felt_base.png, felt_fiber.png, vignette.png, light_pool.png, room_base.png
    Assets/Art/UI/ui_*.png (white, tinted at runtime), icon_*.png, card_shadow.b64.png

Nine-slice borders ride in the file name (".b<N>") and are applied by
Assets/Editor/ArtImportSettings.cs on import.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from premium_common import ART, Image, ensure_dir  # noqa: E402
import premium_cards as cards  # noqa: E402
import premium_chips as chips  # noqa: E402
import premium_ui as ui  # noqa: E402

CARDS = ensure_dir(os.path.join(ART, "Cards"))
CHIPS = ensure_dir(os.path.join(ART, "Chips"))
TABLE = ensure_dir(os.path.join(ART, "Table"))
KIT = ensure_dir(os.path.join(ART, "UI"))


def save(img, folder, name):
    img.save(os.path.join(folder, name + ".png"), optimize=True)


def build_cards():
    for suit in cards.SUITS:
        for rank in range(1, 14):
            save(cards.make_face(suit, rank), CARDS, f"card_{suit}_{rank:02d}")
    save(cards.make_back(), CARDS, "card_Back")
    print("cards: 52 faces + back")


# Store packs, smallest to largest: piles of the house chips.
PACK_PILES = [
    [[100, 100, 25]],
    [[500, 100, 100, 25], [100, 100, 25]],
    [[1000, 500, 500, 100], [500, 100, 100, 25, 25], [100, 25, 25]],
    [[1000, 1000, 1000, 500, 500], [1000, 500, 500, 100, 100, 25], [500, 100, 100, 25], [1000, 1000, 500]],
]


def pack_art(piles, w=320, h=240):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    chip_w = 118
    step = 10
    n = len(piles)
    spread = 86
    shadow = chips.chip_shadow().resize((int(chip_w * 1.2), int(chip_w * 0.5)), Image.LANCZOS)
    # Back piles first so the front ones overlap them.
    order = sorted(range(n), key=lambda i: (0 if i % 2 else 1, i))
    for idx in order:
        pile = piles[idx]
        cx = w / 2 + (idx - (n - 1) / 2) * spread * (0.8 if n > 2 else 1)
        base = h - 18 - (10 if idx % 2 else 0)
        img.alpha_composite(shadow, (int(cx - shadow.width / 2), int(base - shadow.height * 0.7)))
        for k, value in enumerate(reversed(pile)):
            side = chips.chip_side(value)
            side = side.resize((chip_w, int(side.height * chip_w / side.width)), Image.LANCZOS)
            img.alpha_composite(side, (int(cx - chip_w / 2), int(base - side.height - k * step)))
    return img


def build_chips():
    for value in chips.COLOURWAYS:
        save(chips.chip_face(value), CHIPS, f"chip_{value}")
        save(chips.chip_side(value), CHIPS, f"chip_{value}_side")
    save(chips.chip_shadow(), CHIPS, "chip_shadow")
    for i, piles in enumerate(PACK_PILES):
        save(pack_art(piles), CHIPS, f"pack_{i}")
    print(f"chips: {len(chips.COLOURWAYS)} colourways, 4 store packs")


def build_table():
    save(ui.felt_base(), TABLE, "felt_base")
    save(ui.felt_fiber(), TABLE, "felt_fiber")
    save(ui.vignette(), TABLE, "vignette")
    save(ui.light_pool(), TABLE, "light_pool")
    save(ui.room_base(), TABLE, "room_base")
    print("table materials: 5")


def build_kit():
    kit = {
        "ui_rrect.b48": ui.rrect_fill(),
        "ui_rrect_stroke.b48": ui.rrect_stroke(),
        "ui_pill.b64": ui.pill_fill(),
        "ui_pill_stroke.b64": ui.pill_stroke(),
        "ui_circle": ui.circle_fill(),
        "ui_circle_stroke": ui.circle_stroke(),
        "ui_glow": ui.glow(),
        "ui_shadow.b100": ui.soft_rrect_shadow(),
        "ui_focus_ring.b72": ui.focus_ring(),
        "ui_gradient_v": ui.gradient_v(),
        "ui_hairline": ui.hairline_fade(),
        "ui_bet_spot": ui.bet_spot(),
        "card_shadow.b64": ui.card_shadow(),
    }
    for name, img in kit.items():
        save(img, KIT, name)
    for name, fn in ui.ICONS.items():
        save(fn(), KIT, name)
    print(f"kit: {len(kit)} sprites, {len(ui.ICONS)} icons")


if __name__ == "__main__":
    build_kit()
    build_table()
    build_chips()
    build_cards()
