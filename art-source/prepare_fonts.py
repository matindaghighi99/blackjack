#!/usr/bin/env python3
"""
Prepares the two type families the table uses, with the figure styles the UI needs
baked in as the default glyphs.

TextMeshPro renders a font's default glyphs and cannot switch OpenType features on,
so any figure style other than the default has to be frozen into the cmap ahead of
time:

* Inter (UI + numerals) — ``tnum``: tabular figures. Every digit gets the same
  advance, so a balance rolling from 9,875 to 10,250 changes digits in place instead
  of jittering sideways as narrow 1s come and go.
* EB Garamond (display serif) — ``lnum``: lining figures. The default old-style
  figures descend below the baseline, which looks wrong next to capitals
  ("PAYS 3 TO 2").

Both families are SIL OFL 1.1 with no Reserved Font Name. The frozen builds are still
renamed (" Tabular" / " Lining") so they can never be mistaken for the originals.

    apt-get install fonts-inter fonts-ebgaramond     # sources
    pip install fonttools
    python3 art-source/prepare_fonts.py
"""

import os

from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "Fonts")

INTER = "/usr/share/fonts/opentype/inter"
GARAMOND = "/usr/share/fonts/opentype/ebgaramond"

# (source file, output file, features to freeze, family suffix)
BUILDS = [
    (os.path.join(INTER, "Inter-Regular.otf"), "Inter-Regular-Tabular.otf", ["tnum"], " Tabular"),
    (os.path.join(INTER, "Inter-Medium.otf"), "Inter-Medium-Tabular.otf", ["tnum"], " Tabular"),
    (os.path.join(INTER, "Inter-SemiBold.otf"), "Inter-SemiBold-Tabular.otf", ["tnum"], " Tabular"),
    (os.path.join(GARAMOND, "EBGaramond12-Regular.otf"), "EBGaramond-Regular-Lining.otf", ["lnum"], " Lining"),
    (os.path.join(GARAMOND, "EBGaramond12-Italic.otf"), "EBGaramond-Italic-Lining.otf", ["lnum"], " Lining"),
]


def single_substitutions(font, tag):
    """glyph -> glyph for every single-substitution lookup behind feature `tag`."""
    gsub = font["GSUB"].table
    lookup_indices = set()
    for record in gsub.FeatureList.FeatureRecord:
        if record.FeatureTag == tag:
            lookup_indices.update(record.Feature.LookupListIndex)

    mapping = {}
    for index in sorted(lookup_indices):
        lookup = gsub.LookupList.Lookup[index]
        for sub in lookup.SubTable:
            # Extension lookups (type 7) wrap the real subtable.
            if lookup.LookupType == 7:
                sub = sub.ExtSubTable
            if getattr(sub, "LookupType", lookup.LookupType) == 1 and hasattr(sub, "mapping"):
                mapping.update(sub.mapping)
    return mapping


def freeze(font, tags):
    for tag in tags:
        mapping = single_substitutions(font, tag)
        if not mapping:
            raise SystemExit(f"feature '{tag}' has no single substitutions to freeze")
        changed = 0
        for table in font["cmap"].tables:
            for code, glyph in list(table.cmap.items()):
                if glyph in mapping:
                    table.cmap[code] = mapping[glyph]
                    changed += 1
        print(f"  froze {tag}: {changed} cmap entries")


def rename(font, suffix):
    name = font["name"]
    family = name.getDebugName(16) or name.getDebugName(1)
    new_family = family + suffix
    for record in list(name.names):
        if record.nameID in (1, 16):
            name.setName(new_family, record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID in (3, 4, 6):
            old = record.toUnicode()
            if record.nameID == 6:
                new = old.replace(family.replace(" ", ""), new_family.replace(" ", ""), 1)
            else:
                new = old.replace(family, new_family, 1)
            name.setName(new, record.nameID, record.platformID, record.platEncID, record.langID)


def main():
    os.makedirs(OUT, exist_ok=True)
    for source, target, tags, suffix in BUILDS:
        print(os.path.basename(source), "->", target)
        font = TTFont(source)
        freeze(font, tags)
        rename(font, suffix)
        font.save(os.path.join(OUT, target))


if __name__ == "__main__":
    main()
