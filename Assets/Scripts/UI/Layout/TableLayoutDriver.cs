using System;
using BlackjackGame.UI.Components;
using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Layout
{
    /// <summary>
    /// Applies <see cref="TableLayout"/> to the table's RectTransforms whenever the safe
    /// area changes size — rotation, a resized desktop window, a foldable unfolding.
    ///
    /// Everything it places is anchored to the content root's top-left corner, so layout
    /// space (origin top-left, y down) maps directly onto anchoredPosition (x, −y). Seats and
    /// the betting spot depend on the round (how many hands are in play), so the table
    /// screen asks for those through <see cref="Current"/> and places them itself.
    /// </summary>
    public sealed class TableLayoutDriver : MonoBehaviour
    {
        [Tooltip("The rect layout space is measured in (the safe-area content root).")]
        [SerializeField] private RectTransform _root;

        [Header("HUD")]
        [SerializeField] private RectTransform _hudBand;
        [SerializeField] private RectTransform _hudRule;
        [SerializeField] private RectTransform _backButton;
        [SerializeField] private TMP_Text _wordmark;
        [SerializeField] private TMP_Text _session;
        [Tooltip("Right-aligned icon buttons, rightmost first (settings, stats, gift).")]
        [SerializeField] private RectTransform[] _hudIcons;

        [Header("Table")]
        [SerializeField] private FeltArc _rail;
        [SerializeField] private RectTransform _shoe;
        [SerializeField] private RectTransform _discard;
        [SerializeField] private HandView _dealerHand;
        [SerializeField] private RectTransform _dealerBadge;
        [SerializeField] private TMP_Text _feltPrimary;
        [SerializeField] private TMP_Text _feltSecondary;
        [SerializeField] private ResultBanner _banner;
        [SerializeField] private RectTransform _betSpot;
        [SerializeField] private BetChipView _betChip;
        [SerializeField] private RectTransform _status;
        [SerializeField] private RectTransform _lightPool;

        [Header("Dock")]
        [SerializeField] private ChipButton[] _chips;
        [SerializeField] private RectTransform _deal;
        [Tooltip("DOUBLE, HIT, STAND, SPLIT, SURRENDER — in that order.")]
        [SerializeField] private RectTransform[] _actions;
        [Tooltip("BALANCE, BET, LAST readouts.")]
        [SerializeField] private RectTransform[] _stripItems;
        [SerializeField] private TMP_Text[] _stripCaptions;
        [SerializeField] private TMP_Text[] _stripValues;
        [SerializeField] private RectTransform[] _stripDividers;
        [SerializeField] private RectTransform _addChips;
        [SerializeField] private RectTransform _clearBet;

        private Vector2 _appliedSize = new Vector2(-1f, -1f);
        private bool _surrender;
        private bool _dealerHasCards;
        private int _visibleChips = -1;

        /// <summary>The layout most recently applied.</summary>
        public TableLayout Current { get; private set; }

        /// <summary>Raised after every re-layout, with the new geometry.</summary>
        public event Action<TableLayout> Changed;

        private RectTransform Root => _root != null ? _root : (RectTransform)transform;

        private void Start() => ApplyNow();

        private void LateUpdate()
        {
            Vector2 size = Root.rect.size;
            if ((size - _appliedSize).sqrMagnitude > 0.25f) ApplyNow();
        }

        /// <summary>Recomputes and applies the layout immediately.</summary>
        public void ApplyNow() => Apply(Root.rect.size);

        /// <summary>Lays the table out for an explicit size (the scene bootstrapper uses this
        /// to bake a sensible editor preview).</summary>
        public void Apply(Vector2 size)
        {
            if (size.x < 1f || size.y < 1f) return;
            _appliedSize = size;
            var layout = new TableLayout(size.x, size.y);
            Current = layout;

            LayoutHud(layout);
            LayoutTable(layout);
            LayoutDock(layout);
            PlaceDealerBadge();

            Changed?.Invoke(layout);
        }

        /// <summary>Re-flows the action row for four or five buttons.</summary>
        public void SetSurrenderVisible(bool visible)
        {
            if (_surrender == visible) return;
            _surrender = visible;
            if (Current != null) LayoutActions(Current);
        }

        /// <summary>In landscape the dealer's total sits beside the cards, centred while there are none.</summary>
        public void SetDealerHasCards(bool hasCards)
        {
            if (_dealerHasCards == hasCards) return;
            _dealerHasCards = hasCards;
            PlaceDealerBadge();
        }

        /// <summary>How many rack chips the config actually uses; the rest are hidden.</summary>
        public void SetVisibleChipCount(int count)
        {
            _visibleChips = count;
            if (Current != null) LayoutBetRow(Current);
        }

        // =====================================================================

        private void LayoutHud(TableLayout l)
        {
            if (_hudBand != null)
            {
                // Anchored to the top edge and extended far upward, so the band also fills
                // any notch / status-bar inset above the safe area.
                _hudBand.anchorMin = new Vector2(0f, 1f);
                _hudBand.anchorMax = new Vector2(1f, 1f);
                _hudBand.pivot = new Vector2(0.5f, 0f);
                _hudBand.offsetMin = new Vector2(-1000f, -l.HudHeight);
                _hudBand.offsetMax = new Vector2(1000f, -l.HudHeight + 1200f);
            }
            Place(_hudRule, l.CenterX, l.HudHeight, l.Width * 0.9f, 2f);

            const float edge = 24f + 32f;
            Place(_backButton, edge, l.HudY);
            if (_hudIcons != null)
            {
                for (int i = 0; i < _hudIcons.Length; i++)
                    Place(_hudIcons[i], l.Width - edge - i * 80f, l.HudY);
            }

            if (_wordmark != null)
            {
                Place(_wordmark.rectTransform, l.CenterX, l.HudY, 520f, 70f);
                _wordmark.fontSize = l.WordmarkSize;
            }

            if (_session != null)
            {
                _session.gameObject.SetActive(l.ShowSessionLine);
                RectTransform rt = _session.rectTransform;
                SetTopLeft(rt, new Vector2(0f, 0.5f));
                rt.sizeDelta = new Vector2(Mathf.Max(10f, l.CenterX - 300f - 112f), 40f);
                rt.anchoredPosition = new Vector2(112f, -l.HudY);
            }
        }

        private void LayoutTable(TableLayout l)
        {
            if (_rail != null)
            {
                Stretch((RectTransform)_rail.transform);
                _rail.Configure(l.RailY, l.RailSag, l.RailHalfChord, l.RailThickness);
            }

            float shoeCard = l.DealerCardHeight * TableLayout.ShoeScale;
            Place(_shoe, l.Shoe.X, l.Shoe.Y, shoeCard * TableLayout.CardAspect, shoeCard);
            Place(_discard, l.Width - l.Shoe.X, l.Shoe.Y, 10f, 10f);

            if (_dealerHand != null)
            {
                Place((RectTransform)_dealerHand.transform, l.CenterX, l.DealerY, 10f, 10f);
                float h = l.DealerCardHeight;
                _dealerHand.SetLayout(new Vector2(h * TableLayout.CardAspect, h), l.DealerMaxSpan);
            }

            if (_lightPool != null)
            {
                float poolW = l.Width * (l.Portrait ? 1.25f : 0.95f);
                float poolH = (l.TableBottom - l.TableTop) * (l.Portrait ? 0.85f : 1.1f);
                Place(_lightPool, l.CenterX, (l.TableTop + l.TableBottom) / 2f, poolW, poolH);
            }

            PlaceFelt(_feltPrimary, l.PrintPrimaryY, l.PrintPrimarySize, l.ShowPrint, l);
            PlaceFelt(_feltSecondary, l.PrintSecondaryY, l.PrintSecondarySize, l.ShowPrintSecondLine, l);

            if (_banner != null)
            {
                Place((RectTransform)_banner.transform, l.CenterX, l.BannerY, l.Width, l.BannerHeight);
                _banner.SetScale(l.BannerScale);
            }

            Place(_status, l.CenterX, l.RailY + 3f, Mathf.Min(l.Width - 40f, 900f), 36f);
        }

        private void PlaceFelt(TMP_Text text, float y, float size, bool visible, TableLayout l)
        {
            if (text == null) return;
            text.gameObject.SetActive(visible);
            Place(text.rectTransform, l.CenterX, y, l.Width * 0.95f, size * 1.6f);
            text.fontSize = size;
            var curve = text.GetComponent<CurvedText>();
            if (curve != null) curve.SetRadius(l.PrintRadius);
        }

        private void PlaceDealerBadge()
        {
            if (_dealerBadge == null || Current == null) return;
            LayoutPoint p = _dealerHasCards ? Current.DealerLabel : Current.DealerLabelIdle;
            Place(_dealerBadge, p.X, p.Y);
        }

        /// <summary>Places the betting spot and the stake on it (depends on the hand count).</summary>
        public void PlaceBetSpot(LayoutPoint point, bool instant)
        {
            if (Current == null) return;
            float spotWidth = Current.Portrait ? 250f : 230f;
            Place(_betSpot, point.X, point.Y, spotWidth, spotWidth * 0.56f);
            if (_betChip != null)
            {
                var rt = (RectTransform)_betChip.transform;
                SetTopLeft(rt, new Vector2(0.5f, 0.5f));
                rt.sizeDelta = new Vector2(spotWidth, spotWidth * 0.56f);
                _betChip.SetRestPosition(new Vector2(point.X, -(point.Y + 6f)), instant);
                _betChip.SetChipWidth(Current.Portrait ? 128f : 120f);
            }
        }

        private void LayoutDock(TableLayout l)
        {
            LayoutBetRow(l);
            LayoutActions(l);

            LayoutPoint[] items = l.StripItems();
            for (int i = 0; _stripItems != null && i < _stripItems.Length && i < items.Length; i++)
            {
                RectTransform rt = _stripItems[i];
                if (rt == null) continue;

                // Portrait: three centred columns. Landscape: balance hugs the left edge,
                // bet and last the right.
                float pivotX = l.Portrait ? 0.5f : i == 0 ? 0f : 1f;
                TextAlignmentOptions align = l.Portrait
                    ? TextAlignmentOptions.Center
                    : i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;

                SetTopLeft(rt, new Vector2(pivotX, 0.5f));
                rt.sizeDelta = new Vector2(l.Portrait ? l.ControlWidth * 0.3f : 160f, l.StripHeight);
                rt.anchoredPosition = new Vector2(items[i].X, -items[i].Y);

                if (_stripCaptions != null && i < _stripCaptions.Length && _stripCaptions[i] != null)
                    _stripCaptions[i].alignment = align;
                if (_stripValues != null && i < _stripValues.Length && _stripValues[i] != null)
                    _stripValues[i].alignment = align;
            }

            if (_stripDividers != null)
            {
                float step = l.ControlWidth / 6f;
                for (int i = 0; i < _stripDividers.Length; i++)
                {
                    if (_stripDividers[i] == null) continue;
                    _stripDividers[i].gameObject.SetActive(l.Portrait);
                    // Rotated hairlines: the bootstrapper sizes them, only move them here.
                    Place(_stripDividers[i], l.CenterX + (i == 0 ? -step : step), l.StripY);
                }
            }

            // Add-chips sits beside the balance it tops up.
            if (l.Portrait)
            {
                float step = l.ControlWidth * 0.33f;
                Place(_addChips, l.CenterX - step + step * 0.42f, l.StripY + 13f);
            }
            else
            {
                Place(_addChips, 40f + 212f, l.StripY + 13f);
            }
        }

        private void LayoutBetRow(TableLayout l)
        {
            int count = _chips == null ? 0 : _visibleChips < 0 ? _chips.Length : Mathf.Min(_visibleChips, _chips.Length);
            Slot[] slots = l.BetRow(count);
            for (int i = 0; _chips != null && i < _chips.Length; i++)
            {
                if (_chips[i] == null) continue;
                bool used = i < count;
                _chips[i].gameObject.SetActive(used);
                if (used) Place((RectTransform)_chips[i].transform, slots[i].X, l.ControlY, slots[i].Width, slots[i].Height);
            }
            Slot clear = slots[slots.Length - 2];
            Place(_clearBet, clear.X, l.ControlY, clear.Width, clear.Height);
            Slot deal = slots[slots.Length - 1];
            Place(_deal, deal.X, l.ControlY, deal.Width, deal.Height);
        }

        private void LayoutActions(TableLayout l)
        {
            if (_actions == null) return;
            Slot[] slots = l.ActionRow(_surrender);
            for (int i = 0; i < _actions.Length; i++)
            {
                if (_actions[i] == null) continue;
                bool used = i < slots.Length;
                if (i == 4) _actions[i].gameObject.SetActive(used);
                if (used) Place(_actions[i], slots[i].X, l.ControlY, slots[i].Width, slots[i].Height);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Anchors to the root's top-left and positions the centre at (x, y) in layout space.</summary>
        public static void Place(RectTransform rt, float x, float y, float width = -1f, float height = -1f)
        {
            if (rt == null) return;
            SetTopLeft(rt, new Vector2(0.5f, 0.5f));
            if (width >= 0f && height >= 0f) rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>Layout-space point to an anchoredPosition under the root (top-left anchoring).</summary>
        public static Vector2 ToAnchored(LayoutPoint p) => new Vector2(p.X, -p.Y);

        private static void SetTopLeft(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
