using System.Collections.Generic;
using BlackjackGame.UI.Feedback;
using BlackjackGame.UI.Presentation;
using BlackjackGame.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// The stake on the felt, as a physical stack of chips.
    ///
    /// Each chip tapped in the rack is thrown onto the stack, which then re-composes into
    /// the fewest real denominations (the way a dealer "colours up"). At settlement a won
    /// round brings the dealer's payout across to sit beside the stake before both slide
    /// home to the balance; a push slides the stake home; a loss is swept to the dealer.
    /// Chips moving between the player and the house is the clearest way to show where
    /// the money went — a number changing in a corner is not.
    ///
    /// Presentational only: amounts are supplied by the table screen from the engine and
    /// <c>GameManager</c>; nothing here decides what a stake or payout is.
    /// </summary>
    public sealed class BetChipView : MonoBehaviour
    {
        public enum SettleKind
        {
            /// <summary>The stake is lost: swept to the dealer.</summary>
            Lose,
            /// <summary>The stake comes back (push, surrender): slid home.</summary>
            Return,
            /// <summary>The stake comes back with winnings paid beside it.</summary>
            Win,
        }

        [SerializeField] private ChipSpriteLibrary _library;
        [Tooltip("Holds the stake's chips. Moves as one during sweeps.")]
        [SerializeField] private RectTransform _stack;
        [Tooltip("Holds the winnings the dealer pays out beside the stake.")]
        [SerializeField] private RectTransform _payout;
        [Tooltip("A single chip that travels from the rack to the stack on each tap.")]
        [SerializeField] private Image _flyer;

        [Header("Stack")]
        [SerializeField] private float _chipWidth = 124f;
        [Tooltip("Vertical step between stacked chips, in canvas units.")]
        [SerializeField] private float _chipStep = 10f;
        [SerializeField] private int _maxChips = 10;

        [Header("Timing")]
        [SerializeField] private float _placeDuration = 0.34f;
        [SerializeField] private float _payoutDuration = 0.5f;
        [SerializeField] private float _payoutHold = 0.5f;
        [SerializeField] private float _sweepDuration = 0.5f;

        private enum Phase { Idle, PayoutIn, Hold, Sweep }

        private readonly List<Image> _stackChips = new List<Image>();
        private readonly List<Image> _payoutChips = new List<Image>();
        private Image _stackShadow;
        private Image _payoutShadow;
        private CanvasGroup _stackGroup;
        private CanvasGroup _payoutGroup;
        private RectTransform _rect;

        private int[] _denominations = { 10, 25, 100, 500, 1000 };
        private long _amount;

        private bool _haveHomes;
        private Vector3 _balanceWorld;
        private Vector3 _dealerWorld;

        private Vector2 _restTarget;
        private bool _restInitialised;

        private bool _flying;
        private float _flyT;
        private Vector2 _flyFrom;
        private long _flyAmount;

        private float _punch;

        private Phase _phase;
        private float _t;
        private SettleKind _kind;
        private Vector2 _sweepTo;

        /// <summary>The amount currently shown on the felt (after any chip in flight lands).</summary>
        public long Amount => _flying ? _flyAmount : _amount;

        /// <summary>True while a stake is on the felt (or landing on it).</summary>
        public bool IsOnTable => Amount > 0 && _phase == Phase.Idle;

        /// <summary>True while chips are moving.</summary>
        public bool IsBusy => _flying || _phase != Phase.Idle;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _stackGroup = EnsureGroup(_stack);
            _payoutGroup = EnsureGroup(_payout);
            if (_flyer != null) _flyer.enabled = false;
            Hide();
        }

        private static CanvasGroup EnsureGroup(RectTransform root)
        {
            if (root == null) return null;
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        // =====================================================================
        //  Configuration
        // =====================================================================

        /// <summary>The denominations stacks are composed from (from GameConfig).</summary>
        public void SetDenominations(int[] denominations)
        {
            if (denominations != null && denominations.Length > 0) _denominations = denominations;
            Rebuild(_stack, _stackChips, ref _stackShadow, _amount);
        }

        /// <summary>Where chips go home to (the balance readout) and where losses go (the dealer).</summary>
        public void SetHomes(Vector3 balanceWorld, Vector3 dealerWorld)
        {
            _haveHomes = true;
            _balanceWorld = balanceWorld;
            _dealerWorld = dealerWorld;
        }

        /// <summary>Moves the betting spot (layout change, split in landscape). Eases unless instant.</summary>
        public void SetRestPosition(Vector2 anchoredPosition, bool instant)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            _restTarget = anchoredPosition;
            if (instant || !_restInitialised || MotionPrefs.Reduced) _rect.anchoredPosition = anchoredPosition;
            _restInitialised = true;
        }

        public void SetChipWidth(float width)
        {
            _chipWidth = width;
            _chipStep = width * 0.082f;
            Rebuild(_stack, _stackChips, ref _stackShadow, _amount);
        }

        // =====================================================================
        //  Betting
        // =====================================================================

        /// <summary>
        /// Throws one chip of <paramref name="chipValue"/> from <paramref name="fromWorld"/>
        /// (the tapped rack chip, or the balance for a double) onto the stack, which then
        /// shows <paramref name="newAmount"/>.
        /// </summary>
        public void AddChip(long newAmount, int chipValue, Vector3 fromWorld)
        {
            CancelSettle();

            if (_flying) Land(); // a quick second tap lands the first chip at once
            if (_flyer == null || _library == null)
            {
                SetAmount(newAmount);
                return;
            }

            _flyAmount = newAmount;
            _flyFrom = _rect.InverseTransformPoint(fromWorld);
            _flyT = 0f;
            _flying = true;
            _flyer.sprite = _library.Get(chipValue).Side;
            _flyer.enabled = true;
            SizeChip(_flyer, _chipWidth);
            UpdateFlyer();
        }

        /// <summary>Shows <paramref name="amount"/> on the felt without a throw.</summary>
        public void SetAmount(long amount)
        {
            CancelSettle();
            _flying = false;
            if (_flyer != null) _flyer.enabled = false;
            _amount = amount;
            ResetRoots();
            Rebuild(_stack, _stackChips, ref _stackShadow, amount);
        }

        /// <summary>A small settle-kick on the stack — the moment a stake becomes real.</summary>
        public void Punch()
        {
            if (!MotionPrefs.Reduced) _punch = 1f;
        }

        /// <summary>Removes the stake immediately.</summary>
        public void Hide()
        {
            _flying = false;
            if (_flyer != null) _flyer.enabled = false;
            _phase = Phase.Idle;
            _amount = 0;
            ResetRoots();
            Rebuild(_stack, _stackChips, ref _stackShadow, 0);
            Rebuild(_payout, _payoutChips, ref _payoutShadow, 0);
        }

        /// <summary>Takes the stake back to the balance (the player cleared their bet).</summary>
        public void ClearToBalance()
        {
            if (Amount <= 0) return;
            if (_flying) Land();
            StartSweep(SettleKind.Return);
        }

        // =====================================================================
        //  Settlement
        // =====================================================================

        /// <summary>
        /// Plays the stake out. Returns the seconds until the chips reach their destination,
        /// so the caller can roll the balance at the moment they arrive.
        /// </summary>
        public float Settle(SettleKind kind, long winnings)
        {
            if (_flying) Land();
            if (Amount <= 0 && winnings <= 0) return 0f;

            if (kind == SettleKind.Win && winnings > 0)
            {
                _kind = kind;
                Rebuild(_payout, _payoutChips, ref _payoutShadow, winnings);
                _payoutGroup.alpha = 1f;
                _phase = Phase.PayoutIn;
                _t = 0f;
                UpdateSettle();
                return MotionPrefs.Duration(_payoutDuration + _payoutHold + _sweepDuration);
            }

            StartSweep(kind == SettleKind.Win ? SettleKind.Return : kind);
            return MotionPrefs.Duration(_sweepDuration);
        }

        private void StartSweep(SettleKind kind)
        {
            _kind = kind;
            _phase = Phase.Sweep;
            _t = 0f;
            Vector3 target = kind == SettleKind.Lose ? _dealerWorld : _balanceWorld;
            _sweepTo = _haveHomes ? (Vector2)_rect.InverseTransformPoint(target) : new Vector2(0f, 600f);
            UiCues.Raise(kind == SettleKind.Lose ? UiCue.ChipLose : UiCue.ChipCollect);
        }

        private void CancelSettle()
        {
            if (_phase == Phase.Idle) return;
            _phase = Phase.Idle;
            _amount = 0;
            ResetRoots();
            Rebuild(_stack, _stackChips, ref _stackShadow, 0);
            Rebuild(_payout, _payoutChips, ref _payoutShadow, 0);
        }

        // =====================================================================
        //  Animation
        // =====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_restInitialised && (_rect.anchoredPosition - _restTarget).sqrMagnitude > 0.01f)
            {
                _rect.anchoredPosition = MotionPrefs.Reduced
                    ? _restTarget
                    : Ease.Damp(_rect.anchoredPosition, _restTarget, 8f, dt);
            }

            if (_flying)
            {
                _flyT = Mathf.Min(1f, _flyT + dt / Mathf.Max(0.01f, MotionPrefs.Duration(_placeDuration)));
                UpdateFlyer();
                if (_flyT >= 1f) Land();
            }

            if (_punch > 0f && _stack != null)
            {
                _punch = Mathf.Max(0f, _punch - dt / 0.28f);
                float s = 1f + 0.045f * Mathf.Sin(_punch * Mathf.PI);
                _stack.localScale = new Vector3(s, s, 1f);
            }

            if (_phase != Phase.Idle) UpdateSettle(dt);
        }

        private void UpdateFlyer()
        {
            Vector2 to = new Vector2(0f, StackTop(_stackChips.Count));
            float e = Ease.OutCubic(_flyT);
            float hop = MotionPrefs.Amount(70f) * Mathf.Sin(e * Mathf.PI);
            _flyer.rectTransform.anchoredPosition = Vector2.LerpUnclamped(_flyFrom, to, e) + new Vector2(0f, hop);
            float scale = Mathf.Lerp(MotionPrefs.Reduced ? 1f : 0.86f, 1f, e);
            _flyer.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _flyer.rectTransform.SetAsLastSibling();
        }

        private void Land()
        {
            _flying = false;
            if (_flyer != null) _flyer.enabled = false;
            _amount = _flyAmount;
            Rebuild(_stack, _stackChips, ref _stackShadow, _amount);
            Punch();
            UiCues.Raise(UiCue.ChipPlace);
        }

        private void UpdateSettle(float dt = 0f)
        {
            _t += dt;
            switch (_phase)
            {
                case Phase.PayoutIn:
                {
                    float d = MotionPrefs.Duration(_payoutDuration);
                    float e = Ease.OutCubic(Mathf.Clamp01(_t / d));
                    Vector2 from = _haveHomes ? (Vector2)_rect.InverseTransformPoint(_dealerWorld) : new Vector2(0f, 600f);
                    Vector2 home = PayoutHome;
                    _payout.anchoredPosition = Vector2.LerpUnclamped(from, home, e)
                                               + new Vector2(0f, MotionPrefs.Amount(40f) * Mathf.Sin(e * Mathf.PI));
                    if (_t >= d)
                    {
                        _payout.anchoredPosition = home;
                        _phase = Phase.Hold;
                        _t = 0f;
                        UiCues.Raise(UiCue.ChipPlace);
                    }
                    break;
                }

                case Phase.Hold:
                    if (_t >= MotionPrefs.Duration(_payoutHold)) StartSweep(SettleKind.Return);
                    break;

                case Phase.Sweep:
                {
                    float d = MotionPrefs.Duration(_sweepDuration);
                    float k = Mathf.Clamp01(_t / d);
                    // Ease-in: chips accelerate away, which reads as being swept, not placed.
                    float e = Ease.InCubic(k) * 0.6f + k * 0.4f;
                    float scale = Mathf.Lerp(1f, 0.55f, e);
                    float alpha = 1f - Mathf.Clamp01((k - 0.5f) / 0.5f);

                    _stack.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, _sweepTo, e);
                    _stack.localScale = new Vector3(scale, scale, 1f);
                    _stackGroup.alpha = alpha;

                    if (_payoutChips.Count > 0 && _kind != SettleKind.Lose)
                    {
                        _payout.anchoredPosition = Vector2.LerpUnclamped(PayoutHome, _sweepTo, e);
                        _payout.localScale = new Vector3(scale, scale, 1f);
                        _payoutGroup.alpha = alpha;
                    }

                    if (k >= 1f)
                    {
                        _phase = Phase.Idle;
                        _amount = 0;
                        ResetRoots();
                        Rebuild(_stack, _stackChips, ref _stackShadow, 0);
                        Rebuild(_payout, _payoutChips, ref _payoutShadow, 0);
                    }
                    break;
                }
            }
        }

        private Vector2 PayoutHome => new Vector2(_chipWidth * 0.98f, -_chipWidth * 0.06f);

        private void ResetRoots()
        {
            if (_stack != null)
            {
                _stack.anchoredPosition = Vector2.zero;
                _stack.localScale = Vector3.one;
            }
            if (_payout != null)
            {
                _payout.anchoredPosition = PayoutHome;
                _payout.localScale = Vector3.one;
            }
            if (_stackGroup != null) _stackGroup.alpha = 1f;
            if (_payoutGroup != null) _payoutGroup.alpha = 1f;
        }

        // =====================================================================
        //  Stack building
        // =====================================================================

        private float ChipHeight(Sprite sprite, float width) =>
            sprite != null && sprite.rect.width > 0f ? width * sprite.rect.height / sprite.rect.width : width * 0.64f;

        /// <summary>Centre y of the chip that would sit at <paramref name="index"/>.</summary>
        private float StackTop(int index) => index * _chipStep;

        private void Rebuild(RectTransform root, List<Image> pool, ref Image shadow, long amount)
        {
            // Chips are runtime objects; never write them into a scene being built in the editor.
            if (root == null || _library == null || !Application.isPlaying) return;

            List<int> chips = TableText.Breakdown(amount, _denominations, _maxChips);
            if (amount <= 0) chips.Clear();

            if (shadow == null && _library.Shadow != null)
            {
                var go = new GameObject("Shadow", typeof(RectTransform));
                go.transform.SetParent(root, false);
                shadow = go.AddComponent<Image>();
                shadow.sprite = _library.Shadow;
                shadow.raycastTarget = false;
                shadow.color = new Color(0f, 0f, 0f, 0.85f);
            }
            if (shadow != null)
            {
                shadow.enabled = chips.Count > 0;
                shadow.rectTransform.sizeDelta = new Vector2(_chipWidth * 1.3f, _chipWidth * 0.5f);
                shadow.rectTransform.anchoredPosition = new Vector2(0f, -_chipWidth * 0.16f);
                shadow.rectTransform.SetAsFirstSibling();
            }

            while (pool.Count < chips.Count)
            {
                var go = new GameObject($"Chip_{pool.Count:00}", typeof(RectTransform));
                go.transform.SetParent(root, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                pool.Add(image);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                Image image = pool[i];
                bool used = i < chips.Count;
                image.enabled = used;
                if (!used) continue;

                // Bottom of the stack first, so higher chips draw over lower ones.
                image.sprite = _library.Get(chips[i]).Side;
                SizeChip(image, _chipWidth);
                image.rectTransform.anchoredPosition = new Vector2(0f, StackTop(i));
                image.rectTransform.SetAsLastSibling();
            }
        }

        private void SizeChip(Image image, float width)
        {
            image.rectTransform.sizeDelta = new Vector2(width, ChipHeight(image.sprite, width));
        }
    }
}
