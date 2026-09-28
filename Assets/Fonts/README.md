# Bundled fonts

The table uses two families, three weights of one and two styles of the other:

| File | Role | Family | Licence |
|---|---|---|---|
| `EBGaramond-Regular-Lining.otf` | Wordmark, headlines, felt print | EB Garamond 12 | SIL OFL 1.1 — `EBGaramond-OFL.txt` |
| `EBGaramond-Italic-Lining.otf` | Result details, taglines | EB Garamond 12 Italic | SIL OFL 1.1 — `EBGaramond-OFL.txt` |
| `Inter-Regular-Tabular.otf` | Body copy | Inter | SIL OFL 1.1 — `Inter-OFL.txt` |
| `Inter-Medium-Tabular.otf` | Captions, labels | Inter Medium | SIL OFL 1.1 — `Inter-OFL.txt` |
| `Inter-SemiBold-Tabular.otf` | Numbers, buttons, card indices | Inter SemiBold | SIL OFL 1.1 — `Inter-OFL.txt` |

These are **modified versions**. TextMeshPro can't turn OpenType features on, so
`art-source/prepare_fonts.py` bakes the needed figure styles into the default glyphs:

- **Inter** gets tabular figures (`tnum`), so a rolling balance keeps every digit in
  place instead of shifting sideways.
- **EB Garamond** gets lining figures (`lnum`), so "3 TO 2" sits on the baseline next to
  capitals.

Each modified family is renamed ("Tabular" / "Lining") as the OFL asks. Neither family
declares a Reserved Font Name.

The OFL permits bundling in a commercial product as long as the licence travels with the
fonts. **Check the licences yourself before shipping**; the build doesn't check them.

TextMeshPro font assets and the gold gradient preset are generated from these files into
`Assets/Settings/Fonts` by `Blackjack ▸ Rebuild Font Assets`
(`Assets/Editor/FontAssetBuilder.cs`). `Blackjack ▸ Build UI Scenes` also runs it when
any asset is missing. The generated `.asset` files can be deleted and rebuilt at any
time.

To regenerate the fonts themselves:

```bash
apt-get install fonts-inter fonts-ebgaramond
pip install fonttools
python3 art-source/prepare_fonts.py
```
