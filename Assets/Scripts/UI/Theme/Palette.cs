using UnityEngine;

namespace BlackjackGame.UI.Theme
{
    /// <summary>
    /// The one colour system every screen shares: casino black, emerald felt, ivory type
    /// and a dark metallic gold used as an accent — never as a fill for everything.
    ///
    /// Kit sprites are drawn white and tinted from here, so retuning a colour is a one-line
    /// change instead of a re-export. The art pipeline (art-source/premium_common.py) and
    /// the layout preview use the same values.
    /// </summary>
    public static class Palette
    {
        // ---- Surfaces --------------------------------------------------------
        /// <summary>Near-black of the room and the HUD band.</summary>
        public static readonly Color Ink = Hex(0x0B0D0C);
        public static readonly Color Charcoal = Hex(0x161A18);
        /// <summary>Dark glass behind secondary controls and panels (used with alpha).</summary>
        public static readonly Color Glass = Hex(0x0E1110);
        /// <summary>The dock strip below the rail, where the controls sit.</summary>
        public static readonly Color Dock = Hex(0x0A0C0B);

        // ---- Table -----------------------------------------------------------
        public static readonly Color RailDark = Hex(0x090A0A);
        public static readonly Color RailMid = Hex(0x1A1816);
        public static readonly Color RailHighlight = Hex(0x302C28);

        // ---- Accent & type ---------------------------------------------------
        public static readonly Color Gold = Hex(0xC9A45C);
        public static readonly Color GoldLight = Hex(0xE7CF96);
        public static readonly Color GoldDeep = Hex(0x84632D);
        /// <summary>Primary text: warm ivory rather than white, which glares on dark felt.</summary>
        public static readonly Color Ivory = Hex(0xF3EBDD);
        /// <summary>Secondary text: labels such as DEALER, BALANCE.</summary>
        public static readonly Color Muted = Hex(0x969E99);
        /// <summary>Tertiary text: captions and hairline dividers.</summary>
        public static readonly Color Faint = Hex(0x68706B);

        // ---- Outcomes (desaturated so a result never shouts) -------------------
        public static readonly Color Win = Hex(0x86C9A0);
        public static readonly Color Loss = Hex(0xD67C70);
        public static readonly Color Push = Hex(0xBFC5C1);

        /// <summary>A colour from a 0xRRGGBB literal.</summary>
        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        /// <summary>The same colour at a different opacity.</summary>
        public static Color WithAlpha(this Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
    }
}
