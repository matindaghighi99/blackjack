# Bundled fonts

| File | Family | Licence |
|---|---|---|
| `TeXGyreBonum-Bold.otf` | TeX Gyre Bonum (Bookman-style serif) | GUST Font License — see `TeXGyreBonum-LICENSE.txt` |
| `Lato-Bold.ttf`, `Lato-Regular.ttf` | Lato | SIL Open Font License 1.1 |

Both licences permit redistribution in a commercial product. **Verify independently
before shipping** — licence terms are the publisher's responsibility, not the build's.

TextMeshPro font assets and the gold material presets are generated from these files by
`Blackjack ▸ Rebuild Font Assets` (`Assets/Editor/FontAssetBuilder.cs`), so the generated
`.asset` files can be deleted and rebuilt at any time.

To swap the display face, drop a replacement in this folder and update
`FontAssetBuilder.DisplayFontPath`.
