using System;

namespace BlackjackGame.UI.Layout
{
    /// <summary>A point in layout space: origin at the safe area's top-left, y pointing down.</summary>
    public readonly struct LayoutPoint
    {
        public readonly float X;
        public readonly float Y;

        public LayoutPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>Where each player hand goes for a given number of hands.</summary>
    public sealed class SeatLayout
    {
        /// <summary>Centre x of each seat, left to right in engine hand order.</summary>
        public float[] Xs;
        public float Y;
        public float CardHeight;
        /// <summary>Widest each hand may fan, centre-to-centre of its outer cards.</summary>
        public float MaxSpan;
        /// <summary>Total badge position relative to the seat centre.</summary>
        public LayoutPoint BadgeOffset;
        /// <summary>Centre of the betting spot for this arrangement.</summary>
        public LayoutPoint Bet;
    }

    /// <summary>One slot in a control row.</summary>
    public readonly struct Slot
    {
        public readonly float X;
        public readonly float Width;
        public readonly float Height;

        public Slot(float x, float width, float height)
        {
            X = x;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// The table's responsive geometry, computed from nothing but the safe area's size.
    ///
    /// Portrait (phones, tablets upright) stacks dealer, felt print, player and betting spot
    /// vertically. Landscape (desktop, tablets and phones on their side) has height to spare
    /// for nothing, so totals move beside each hand and the betting spot moves to the right,
    /// leaving the band between the hands tall enough for the result banner.
    ///
    /// Pure arithmetic with no Unity types, so it can be checked for overlaps at any screen
    /// size headlessly. art-source/preview_premium_table.py mirrors it line for line; keep
    /// the two in step.
    /// </summary>
    public sealed class TableLayout
    {
        /// <summary>Card art is 2.5 x 3.5 (360 x 504 px).</summary>
        public const float CardAspect = 360f / 504f;

        /// <summary>
        /// Height/width at or above which the stacked (portrait) composition is used. Square-ish
        /// screens (foldables, some tablets) are portrait too: the side-by-side composition needs
        /// real width for its flanks.
        /// </summary>
        public const float PortraitThreshold = 0.9f;

        public readonly float Width;
        public readonly float Height;
        public readonly bool Portrait;
        public readonly float CenterX;

        // HUD
        public readonly float HudHeight;
        public readonly float HudY;
        public readonly bool ShowSessionLine;

        // Dock
        public readonly float ControlHeight;
        public readonly float ControlY;
        public readonly float ControlWidth;
        public readonly float StripHeight;
        public readonly float StripY;
        public readonly float DockTop;

        // Rail (the padded front edge of the table)
        public readonly float RailThickness;
        public readonly float RailSag;
        /// <summary>Rail centre line at the screen's centre, its lowest point.</summary>
        public readonly float RailY;

        // Table zone
        public readonly float TableTop;
        public readonly float TableBottom;

        // Dealer
        public readonly float DealerCardHeight;
        public readonly float DealerY;
        /// <summary>Widest the dealer's hand may fan (kept clear of the shoe and the screen edges).</summary>
        public readonly float DealerMaxSpan;
        public readonly LayoutPoint DealerLabel;
        /// <summary>Where the DEALER label rests while the dealer has no cards.</summary>
        public readonly LayoutPoint DealerLabelIdle;

        // Player (single hand)
        public readonly float PlayerCardHeight;
        public readonly float PlayerY;
        /// <summary>Widest a single player hand may fan.</summary>
        public readonly float PlayerMaxSpan;
        public readonly LayoutPoint BetSpot;

        public readonly LayoutPoint Shoe;

        // Felt print & result banner share the band between the hands.
        public readonly float PrintY;
        public readonly float GapHeight;
        public readonly float BannerY;
        public readonly float BannerHeight;

        public TableLayout(float width, float height)
        {
            Width = Math.Max(1f, width);
            Height = Math.Max(1f, height);
            Portrait = Height / Width >= PortraitThreshold;
            CenterX = Width / 2f;

            // ---- HUD ---------------------------------------------------------
            HudHeight = Portrait ? 112f : 100f;
            HudY = HudHeight / 2f;
            ShowSessionLine = !Portrait && Width >= 1500f;

            // ---- Dock: controls pinned to the bottom of the safe area ----------
            ControlHeight = Portrait ? 116f : 108f;
            ControlY = Height - 30f - ControlHeight / 2f;
            if (Portrait)
            {
                StripHeight = 64f;
                StripY = ControlY - ControlHeight / 2f - 22f - StripHeight / 2f;
                DockTop = StripY - StripHeight / 2f - 18f;
                ControlWidth = Math.Min(Width - 48f, 980f);
            }
            else
            {
                StripHeight = ControlHeight;
                StripY = ControlY;
                DockTop = ControlY - ControlHeight / 2f - 26f;
                // Balance sits on the left flank and bet/last on the right one; the
                // controls take what is left between them.
                ControlWidth = Math.Min(Width - 800f, 900f);
            }

            // ---- Rail --------------------------------------------------------
            RailThickness = Portrait ? 46f : 42f;
            RailSag = Portrait ? 70f : 80f;
            RailY = DockTop - RailThickness / 2f;

            // ---- Table zone ----------------------------------------------------
            TableTop = HudHeight + 8f;
            TableBottom = RailY - RailThickness / 2f - (Portrait ? 10f : 30f);
            float th = TableBottom - TableTop;

            float gapTop, gapBottom;
            if (Portrait)
            {
                DealerCardHeight = Clamp(th * 0.205f, 160f, 310f);
                PlayerCardHeight = Clamp(th * 0.245f, 180f, 360f);
                float dealerLabelY = TableTop + 38f;
                DealerLabel = new LayoutPoint(CenterX, dealerLabelY);
                DealerLabelIdle = DealerLabel;
                DealerY = dealerLabelY + 36f + DealerCardHeight / 2f;
                float betY = TableBottom - 64f;
                BetSpot = new LayoutPoint(CenterX, betY);
                PlayerY = betY - 92f - PlayerCardHeight / 2f;
                Shoe = new LayoutPoint(Width - 100f, TableTop + 92f);
                gapTop = DealerY + DealerCardHeight / 2f;
                gapBottom = PlayerY - PlayerCardHeight / 2f - 36f - 24f;

                // The dealer's hand fans toward the shoe: stop short of it.
                float dealerWidth = CardWidth(DealerCardHeight);
                float shoeLeft = Shoe.X - CardWidth(DealerCardHeight * ShoeScale) / 2f - 12f;
                DealerMaxSpan = Math.Max(dealerWidth * 0.6f,
                    Math.Min(MaxSpan(DealerCardHeight), 2f * (shoeLeft - 16f - CenterX) - dealerWidth));
                float playerWidth = CardWidth(PlayerCardHeight);
                PlayerMaxSpan = Math.Max(playerWidth * 0.6f, Math.Min(MaxSpan(PlayerCardHeight), Width - 48f - playerWidth));
            }
            else
            {
                DealerCardHeight = Clamp(th * 0.28f, 150f, 250f);
                PlayerCardHeight = Clamp(th * 0.33f, 170f, 290f);
                DealerY = TableTop + 20f + DealerCardHeight / 2f;
                PlayerY = TableBottom - 18f - PlayerCardHeight / 2f;
                DealerMaxSpan = MaxSpan(DealerCardHeight);
                DealerLabel = new LayoutPoint(
                    CenterX - (DealerMaxSpan / 2f + CardWidth(DealerCardHeight) / 2f + 110f), DealerY);
                DealerLabelIdle = new LayoutPoint(CenterX, DealerY);

                // The betting spot sits right of the hand at its widest; on narrower
                // landscapes the hand's fan tightens instead of pushing the spot off-screen.
                float playerWidth = CardWidth(PlayerCardHeight);
                PlayerMaxSpan = playerWidth * 2.4f;
                float betX = CenterX + PlayerMaxSpan / 2f + playerWidth / 2f + 150f;
                if (betX > Width - 150f)
                {
                    betX = Width - 150f;
                    PlayerMaxSpan = Math.Max(playerWidth * 0.6f, 2f * (betX - 150f - CenterX) - playerWidth);
                }
                BetSpot = new LayoutPoint(betX, PlayerY + PlayerCardHeight * 0.18f);
                Shoe = new LayoutPoint(Width - 150f, TableTop + 120f);
                gapTop = DealerY + DealerCardHeight / 2f;
                gapBottom = PlayerY - PlayerCardHeight / 2f - 12f;
            }

            PrintY = (gapTop + gapBottom) / 2f;
            GapHeight = gapBottom - gapTop;
            BannerY = PrintY;
            BannerHeight = Clamp(GapHeight - 12f, 120f, 176f);
        }

        // ---------------------------------------------------------------------
        //  Derived geometry
        // ---------------------------------------------------------------------

        /// <summary>Default cap on how wide a hand may fan, centre-to-centre of its outer cards.</summary>
        public static float MaxSpan(float cardHeight) => cardHeight * CardAspect * 2.9f;

        /// <summary>The shoe's cards relative to the dealer's.</summary>
        public const float ShoeScale = 0.55f;

        public static float CardWidth(float cardHeight) => cardHeight * CardAspect;

        /// <summary>The felt print needs this much room to show at all…</summary>
        public bool ShowPrint => GapHeight >= 70f;

        /// <summary>…and this much for its second (dealer rule) line.</summary>
        public bool ShowPrintSecondLine => GapHeight >= 120f;

        public float PrintPrimaryY => ShowPrintSecondLine ? PrintY - 20f : PrintY;
        public float PrintSecondaryY => PrintY + 30f;
        public float PrintPrimarySize => Portrait ? 40f : 36f;
        public float PrintSecondarySize => Portrait ? 30f : 28f;
        /// <summary>Radius of the felt print's arc: gentler than the rail, like real table printing.</summary>
        public float PrintRadius => Width * (Portrait ? 1.25f : 1.9f);

        /// <summary>Half the width the rail's curvature is defined over (it runs past the edges).</summary>
        public float RailHalfChord => Width / 2f * 1.08f;

        /// <summary>Radius of the circle the rail centre line lies on.</summary>
        public float RailRadius => (RailHalfChord * RailHalfChord + RailSag * RailSag) / (2f * RailSag);

        /// <summary>Rail centre line height at horizontal position x (layout space).</summary>
        public float RailYAt(float x)
        {
            float dx = x - CenterX;
            float r = RailRadius;
            return RailY - r + (float)Math.Sqrt(Math.Max(0f, r * r - dx * dx));
        }

        /// <summary>Scale for the result banner's type, from its available height.</summary>
        public float BannerScale => BannerHeight / 176f;

        public float WordmarkSize => Portrait ? 40f : 38f;

        // ---------------------------------------------------------------------
        //  Seats
        // ---------------------------------------------------------------------

        /// <summary>Per-seat layout for <paramref name="count"/> player hands.</summary>
        public SeatLayout Seats(int count)
        {
            float fullWidth = CardWidth(PlayerCardHeight);
            if (count <= 1)
            {
                float span = PlayerMaxSpan;
                return new SeatLayout
                {
                    Xs = new[] { CenterX },
                    Y = PlayerY,
                    CardHeight = PlayerCardHeight,
                    MaxSpan = span,
                    BadgeOffset = Portrait
                        ? new LayoutPoint(0f, -PlayerCardHeight / 2f - 36f)
                        : new LayoutPoint(-(span / 2f + fullWidth / 2f + 110f), 0f),
                    Bet = BetSpot,
                };
            }

            float shrink, usable, centre;
            if (Portrait)
            {
                // Narrow screens: more hands means smaller cards, so each hand can still fan
                // wide enough to show every card's index.
                shrink = count == 2 ? 0.86f : count == 3 ? 0.72f : 0.56f;
                usable = Width - 60f;
                centre = CenterX;
            }
            else
            {
                shrink = count == 2 ? 0.9f : count == 3 ? 0.8f : 0.72f;
                // Keep the right-hand end of the band free for the betting spot.
                usable = Math.Min(Width - 120f - 320f, 1300f);
                centre = CenterX - 140f;
            }

            float spacing = usable / count;
            // Also cap the cards so a three-card hand fans wide enough (a quarter-card step)
            // to show every index within its seat.
            float fitHeight = (spacing - 24f) / 1.52f / CardAspect;
            float cardHeight = Math.Min(PlayerCardHeight * shrink, fitHeight);
            var xs = new float[count];
            for (int i = 0; i < count; i++) xs[i] = centre + (i - (count - 1) / 2f) * spacing;

            LayoutPoint bet = Portrait
                ? BetSpot
                : new LayoutPoint(xs[count - 1] + spacing / 2f + 150f, BetSpot.Y);

            return new SeatLayout
            {
                Xs = xs,
                Y = PlayerY,
                CardHeight = cardHeight,
                MaxSpan = Math.Max(0f, spacing - CardWidth(cardHeight) - 24f),
                BadgeOffset = new LayoutPoint(0f, -cardHeight / 2f - 30f),
                Bet = bet,
            };
        }

        // ---------------------------------------------------------------------
        //  Dock rows
        // ---------------------------------------------------------------------

        public float ControlLeft => CenterX - ControlWidth / 2f;

        /// <summary>Chip rack, then CLEAR, then DEAL, left to right.</summary>
        public Slot[] BetRow(int chipCount)
        {
            chipCount = Math.Max(0, chipCount);
            const float gap = 12f;
            float dealWidth = ControlWidth * 0.28f;
            float clearWidth = ControlHeight * 0.62f;
            float chipDiameter = chipCount == 0
                ? 0f
                : Math.Min(ControlHeight * 0.92f,
                    (ControlWidth - dealWidth - clearWidth - 30f - chipCount * gap) / chipCount);

            var slots = new Slot[chipCount + 2];
            float x = ControlLeft + chipDiameter / 2f;
            for (int i = 0; i < chipCount; i++)
            {
                slots[i] = new Slot(x, chipDiameter, chipDiameter);
                x += chipDiameter + gap;
            }
            float dealX = ControlLeft + ControlWidth - dealWidth / 2f;
            slots[chipCount] = new Slot(dealX - dealWidth / 2f - 18f - clearWidth / 2f, clearWidth, clearWidth);
            slots[chipCount + 1] = new Slot(dealX, dealWidth, ControlHeight);
            return slots;
        }

        /// <summary>
        /// DOUBLE, HIT, STAND, SPLIT — plus SURRENDER at the end when the rules offer it.
        /// HIT and STAND are the decisions made on almost every hand, so they get the wide
        /// primary slots in the middle, under the thumb.
        /// </summary>
        public Slot[] ActionRow(bool withSurrender)
        {
            const float gap = 14f;
            int count = withSurrender ? 5 : 4;
            float usable = ControlWidth - gap * (count - 1);
            float secondary = usable * (withSurrender ? 0.16f : 0.2f);
            float primary = usable * (withSurrender ? 0.26f : 0.3f);
            float secondaryHeight = ControlHeight * 0.9f;

            float[] widths = withSurrender
                ? new[] { secondary, primary, primary, secondary, secondary }
                : new[] { secondary, primary, primary, secondary };

            var slots = new Slot[count];
            float x = ControlLeft;
            for (int i = 0; i < count; i++)
            {
                bool isPrimary = i == 1 || i == 2;
                slots[i] = new Slot(x + widths[i] / 2f, widths[i], isPrimary ? ControlHeight : secondaryHeight);
                x += widths[i] + gap;
            }
            return slots;
        }

        /// <summary>BALANCE, BET, LAST readouts: three columns in portrait, flanks in landscape.</summary>
        public LayoutPoint[] StripItems()
        {
            if (Portrait)
            {
                float step = ControlWidth * 0.33f;
                return new[]
                {
                    new LayoutPoint(CenterX - step, StripY),
                    new LayoutPoint(CenterX, StripY),
                    new LayoutPoint(CenterX + step, StripY),
                };
            }

            return new[]
            {
                new LayoutPoint(40f, StripY),           // BALANCE, left-aligned
                new LayoutPoint(Width - 210f, StripY),  // BET, right-aligned
                new LayoutPoint(Width - 40f, StripY),   // LAST, right-aligned
            };
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
