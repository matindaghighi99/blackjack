using System;
using System.Collections;
using System.Collections.Generic;
using BlackjackGame.Blackjack;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Core;
using BlackjackGame.Economy;
using BlackjackGame.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Screens
{
    /// <summary>
    /// Game-table layout and controls.
    ///
    /// Betting is chip-first: tapping a denomination throws chips onto the felt and the
    /// stake accumulates there — no keyboard, ever. The resting stack is tappable to
    /// clear, and the last stake is re-placed automatically after a round so DEAL alone
    /// repeats it.
    ///
    /// Settling is paced by <see cref="SettleSequence"/>: the hole card turns, the
    /// dealer draws card by card, and only then does the outcome land — the engine
    /// resolves instantly, but the table never lets the answer arrive before the story.
    ///
    /// Split hands each get their own <see cref="HandView"/> with a result badge; the
    /// hand in play is emphasised, the waiting one recedes.
    /// </summary>
    public sealed class GameTableUI : MonoBehaviour
    {
        [Header("Bet Controls")]
        [Tooltip("Denomination chip buttons, smallest to largest.")]
        [SerializeField] private Button[] _chipButtons;
        [Tooltip("Chip values matching _chipButtons, index for index.")]
        [SerializeField] private int[] _chipValues = { 100, 500, 1000, 5000 };
        [SerializeField] private Button _dealButton;

        [Header("Row Swapping")]
        [Tooltip("Betting controls — shown between rounds.")]
        [SerializeField] private CanvasGroup _betRow;
        [Tooltip("Hit/Stand/Double/Split — shown while a round is live.")]
        [SerializeField] private CanvasGroup _actionRow;

        [Header("Action Buttons")]
        [SerializeField] private Button _hitButton;
        [SerializeField] private Button _standButton;
        [SerializeField] private Button _doubleButton;
        [SerializeField] private Button _splitButton;

        [Header("Cards")]
        [SerializeField] private HandView _dealerHandView;
        [Tooltip("The single-hand view, centred on the felt.")]
        [SerializeField] private HandView _playerHandView;
        [Tooltip("Left and right views used while a split is in play.")]
        [SerializeField] private HandView _splitHandLeft;
        [SerializeField] private HandView _splitHandRight;

        [Header("Split Badges")]
        [SerializeField] private TMP_Text _splitBadgeLeft;
        [SerializeField] private TMP_Text _splitBadgeRight;

        [Header("Display")]
        [SerializeField] private TMP_Text _dealerHandLabel;
        [SerializeField] private TMP_Text _playerHandLabel;
        [SerializeField] private TMP_Text _outcomeLabel;
        [SerializeField] private TMP_Text _balanceLabel;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;
        [Tooltip("The + on the balance pill — shortcut to the chip store.")]
        [SerializeField] private Button _addChipsButton;

        [Header("Quick Actions (top bar)")]
        [SerializeField] private Button _giftButton;
        [SerializeField] private PulsingDot _giftDot;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _trophyButton;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private StatsPanel _statsPanel;

        [Header("Juice")]
        [Tooltip("Rolls the balance up instead of snapping it.")]
        [SerializeField] private CountRollup _balanceRollup;
        [Tooltip("Slams the outcome text in when a round settles.")]
        [SerializeField] private LabelPunch _outcomePunch;
        [Tooltip("Shakes the table on blackjacks and busts.")]
        [SerializeField] private ScreenShake _shake;
        [Tooltip("The stake on the felt — grows as chips are tapped, settles with the round.")]
        [SerializeField] private BetChipView _betChip;
        [Tooltip("Button layered on the bet chip so the resting stack can be tapped clear.")]
        [SerializeField] private Button _betChipButton;

        [Header("Pacing")]
        [Tooltip("Pause after the hole card turns, before the dealer draws.")]
        [SerializeField] private float _revealPause = 0.45f;
        [Tooltip("Pause between successive dealer draws.")]
        [SerializeField] private float _drawPause = 0.32f;

        /// <summary>Outcome tints. Gold for a blackjack, green win, red loss, grey push.</summary>
        private static readonly Color WinColor = new Color(0.42f, 1f, 0.55f);
        private static readonly Color LoseColor = new Color(1f, 0.38f, 0.38f);
        private static readonly Color BlackjackColor = new Color(1f, 0.86f, 0.35f);
        private static readonly Color PushColor = new Color(0.85f, 0.85f, 0.85f);

        private GameManager _game;

        /// <summary>The engine instance the views were last drawn for; a change means "new round".</summary>
        private BlackjackEngine _renderedEngine;

        /// <summary>Results delivered by <see cref="GameManager.OnRoundComplete"/> for the current round.</summary>
        private IReadOnlyList<HandResult> _lastResults;

        private int _currentBet;
        private int _lastBet;
        private bool _splitLayout;

        /// <summary>True while the settle sequence coroutine owns the table.</summary>
        private bool _settling;
        /// <summary>True once this round's outcome reaction has played, so it fires once.</summary>
        private bool _outcomeShown;

        private void Start()
        {
            _game = GameManager.Instance;

            if (_dealButton != null) _dealButton.onClick.AddListener(OnDeal);
            if (_hitButton != null) _hitButton.onClick.AddListener(() => { _game.Hit(); Refresh(); });
            if (_standButton != null) _standButton.onClick.AddListener(() => { _game.Stand(); Refresh(); });
            if (_doubleButton != null) _doubleButton.onClick.AddListener(() => { _game.DoubleDown(); Refresh(); });
            if (_splitButton != null) _splitButton.onClick.AddListener(() => { _game.Split(); Refresh(); });
            if (_backButton != null)
                _backButton.onClick.AddListener(() => _game.GoToScene(SceneNames.MainMenu));
            if (_addChipsButton != null)
                _addChipsButton.onClick.AddListener(() => _game.GoToScene(SceneNames.Store));

            if (_chipButtons != null)
            {
                for (int i = 0; i < _chipButtons.Length; i++)
                {
                    int index = i; // avoid closure capture bug
                    if (_chipButtons[i] != null)
                        _chipButtons[i].onClick.AddListener(() => OnChipTapped(index));
                }
            }
            // The stake itself is the display: it grows on the felt as chips are
            // tapped, and tapping it clears the bet.
            if (_betChipButton != null) _betChipButton.onClick.AddListener(ClearBet);

            if (_giftButton != null) _giftButton.onClick.AddListener(ClaimGift);
            if (_settingsButton != null && _settingsPanel != null)
                _settingsButton.onClick.AddListener(_settingsPanel.Show);
            if (_trophyButton != null && _statsPanel != null)
                _trophyButton.onClick.AddListener(_statsPanel.Show);

            if (_game != null)
                _game.OnRoundComplete += results => { _lastResults = results; Refresh(); };

            // The chip flies home to wherever the balance pill actually is on this
            // device, safe area included — not to a baked reference-canvas guess.
            if (_betChip != null && _balanceLabel != null)
                _betChip.SetPillPosition(ToChipSpace(_balanceLabel.rectTransform));

            RefreshGiftDot();
            Refresh();
        }

        // =====================================================================
        //  Betting
        // =====================================================================

        private int MinBet =>
            AppManager.Exists && AppManager.Instance.GameConfig != null
                ? AppManager.Instance.GameConfig.MinBet
                : 1;

        /// <summary>Highest stake the player can legally place *and* afford.</summary>
        private int MaxBet
        {
            get
            {
                int ceiling = AppManager.Exists && AppManager.Instance.GameConfig != null
                    ? AppManager.Instance.GameConfig.MaxBet
                    : int.MaxValue;
                long affordable = _game != null ? _game.Balance : 0;
                return (int)Math.Min(ceiling, Math.Max(0, affordable));
            }
        }

        private void OnChipTapped(int index)
        {
            if (_settling || RoundInProgress) return;
            if (_chipValues == null || index >= _chipValues.Length) return;

            int value = _chipValues[index];
            int next = Mathf.Min(_currentBet + value, MaxBet);

            if (next <= _currentBet)
            {
                // Couldn't add even part of the chip — the wall is the balance, not the
                // table limit, whenever the balance is what's binding.
                NotEnoughChips();
                return;
            }

            _currentBet = next;

            if (_betChip != null)
            {
                Vector2 origin = _chipButtons != null && index < _chipButtons.Length && _chipButtons[index] != null
                    ? ToChipSpace((RectTransform)_chipButtons[index].transform)
                    : Vector2.zero;
                _betChip.ShowPreview(_currentBet, origin);
            }

            RefreshBetControls();
        }

        private void ClearBet()
        {
            if (_settling || RoundInProgress) return;
            if (_currentBet == 0) return;

            _currentBet = 0;
            if (_betChip != null) _betChip.Hide();
            RefreshBetControls();
        }

        private void OnDeal()
        {
            if (_settling || _currentBet < MinBet) return;

            // Reset the round state BEFORE dealing: an instant blackjack settles inside
            // PlaceBetAndDeal, and its results event must land on a clean slate rather
            // than be wiped by resets running after it.
            _lastResults = null;
            _outcomeShown = false;
            if (_outcomeLabel != null) _outcomeLabel.text = "";
            if (_outcomePunch != null) _outcomePunch.ResetNow();

            bool placed = _game.PlaceBetAndDeal(_currentBet);
            if (!placed)
            {
                NotEnoughChips();
                return;
            }

            _lastBet = _currentBet;

            // The stake is already resting on the felt from the preview; give the stack
            // a settle-kick to mark the moment it becomes real.
            if (_betChip != null) _betChip.Punch();
            if (_betChipButton != null) _betChipButton.interactable = false;

            Refresh();
        }

        private void NotEnoughChips()
        {
            if (_outcomeLabel != null) _outcomeLabel.text = "NOT ENOUGH CHIPS";
            if (_outcomePunch != null) _outcomePunch.Play(LoseColor, 0.5f);
            if (_shake != null) _shake.Shake(0.25f);
        }

        /// <summary>Re-places the last stake (clamped to what's affordable) after a round,
        /// so a bare DEAL tap repeats the bet.</summary>
        private void PrefillRebet()
        {
            _currentBet = Mathf.Clamp(_lastBet, 0, MaxBet);
            if (_currentBet < MinBet) _currentBet = 0;

            if (_betChip != null)
            {
                if (_currentBet > 0)
                {
                    Vector2 origin = _balanceLabel != null
                        ? ToChipSpace(_balanceLabel.rectTransform)
                        : Vector2.zero;
                    _betChip.ShowPreview(_currentBet, origin);
                }
                else
                {
                    _betChip.Hide();
                }
            }
            if (_betChipButton != null) _betChipButton.interactable = true;
        }

        private void RefreshBetControls()
        {
            bool broke = MaxBet < MinBet;

            if (_dealButton != null) _dealButton.interactable = !broke && _currentBet >= MinBet;

            if (_chipButtons != null && _chipValues != null)
            {
                for (int i = 0; i < _chipButtons.Length && i < _chipValues.Length; i++)
                {
                    if (_chipButtons[i] == null) continue;
                    _chipButtons[i].interactable = !broke && _currentBet < MaxBet;
                }
            }
        }

        private bool RoundInProgress =>
            _game != null && _game.Engine != null &&
            _game.Engine.Phase != RoundPhase.Settled && _game.Engine.Phase != RoundPhase.Idle;

        // =====================================================================
        //  Daily reward shortcut
        // =====================================================================

        /// <summary>Same claim flow as the Main Menu's daily-reward button. Result is
        /// shown in the outcome label — it's free between rounds and gets overwritten by
        /// the next round's result either way.</summary>
        private void ClaimGift()
        {
            if (!AppManager.Exists || _outcomeLabel == null) return;

            DailyRewardResult result = AppManager.Instance.Rewards.TryClaim(DateTime.UtcNow);
            _outcomeLabel.text = result.Success
                ? $"+{result.ChipsAwarded:N0} chips! Streak: {result.NewStreak}"
                : $"Next reward in {result.TimeUntilNext.Hours}h {result.TimeUntilNext.Minutes}m";
            if (_outcomePunch != null && result.Success)
                _outcomePunch.Play(BlackjackColor, 0.6f);

            RefreshGiftDot();
            if (!_settling) RefreshBalance();
            RefreshBetControls();
        }

        private void RefreshGiftDot()
        {
            if (_giftDot == null || !AppManager.Exists) return;
            if (AppManager.Instance.Rewards.IsRewardAvailable(DateTime.UtcNow)) _giftDot.Show();
            else _giftDot.Hide();
        }

        // =====================================================================
        //  Rendering
        // =====================================================================

        private void RefreshBalance()
        {
            if (_game == null) return;
            // The rollup owns the label's text when present, so don't write both.
            if (_balanceRollup != null) _balanceRollup.SetValue(_game.Balance);
            else if (_balanceLabel != null) _balanceLabel.text = $"{_game.Balance:N0}";
        }

        private void Refresh()
        {
            // While the settle sequence runs it owns the dealer, the balance and the
            // rows; stray Refresh calls (button handlers, round-complete event) must not
            // fight it.
            if (_settling) return;

            RefreshBalance();

            var engine = _game.Engine;
            if (engine == null)
            {
                SetActionsInteractable(false);
                if (_dealerHandView != null) _dealerHandView.Clear();
                ClearPlayerViews();
                if (_dealerHandLabel != null) _dealerHandLabel.text = "Dealer";
                if (_playerHandLabel != null)
                    _playerHandLabel.text = MaxBet < MinBet
                        ? "Out of chips — tap + for more"
                        : "Tap a chip to place your bet";
                if (_betChip != null && _currentBet == 0) _betChip.Hide();
                RefreshBetControls();
                ShowRow(_betRow, true);
                ShowRow(_actionRow, false);
                return;
            }

            // A settled round that hasn't reacted yet: freeze the pre-reveal picture and
            // hand the table to the sequence. The player's last card still animates in;
            // the dealer stays concealed until the sequence turns the hole card.
            if (engine.Phase == RoundPhase.Settled && !_outcomeShown)
            {
                _settling = true;
                SyncNewRound(engine);
                RenderPlayer(engine);
                RenderDealerConcealed(engine);
                ShowRow(_betRow, false);
                ShowRow(_actionRow, false);
                StartCoroutine(SettleSequence(engine));
                return;
            }

            SyncNewRound(engine);
            RenderPlayer(engine);

            bool playing = engine.Phase == RoundPhase.PlayerTurn;
            if (playing) RenderDealerConcealed(engine);
            else RenderDealer(engine, engine.DealerHand.Cards.Count);

            // Keep the chip's number honest: doubling and splitting both change how much
            // is actually at risk, so total it from the hands rather than the opening bet.
            // Only while the round owns the chip — after settle the chip previews the
            // NEXT stake, and last round's total must not overwrite it.
            if (_betChip != null && engine.Phase == RoundPhase.PlayerTurn)
            {
                long staked = 0;
                foreach (Hand h in engine.PlayerHands) staked += h.Bet;
                if (staked > 0) _betChip.SetAmount(staked);
            }

            Hand active = engine.ActiveHand;
            bool canAffordExtra = active != null && _game.Balance >= active.Bet;
            if (_hitButton != null) _hitButton.interactable = engine.CanHit;
            if (_standButton != null) _standButton.interactable = engine.CanStand;
            if (_doubleButton != null) _doubleButton.interactable = engine.CanDouble && canAffordExtra;
            if (_splitButton != null) _splitButton.interactable = engine.CanSplit && canAffordExtra;
            if (_dealButton != null) _dealButton.interactable = !playing && _currentBet >= MinBet;

            if (!playing) RefreshBetControls();
            ShowRow(_betRow, !playing);
            ShowRow(_actionRow, playing);
        }

        /// <summary>Clears the card views when a fresh engine appears, so the new round
        /// deals in rather than snapping.</summary>
        private void SyncNewRound(BlackjackEngine engine)
        {
            if (ReferenceEquals(engine, _renderedEngine)) return;

            if (_dealerHandView != null) _dealerHandView.Clear();
            ClearPlayerViews();
            _splitLayout = false;
            _renderedEngine = engine;
        }

        private void ClearPlayerViews()
        {
            if (_playerHandView != null) _playerHandView.Clear();
            if (_splitHandLeft != null) _splitHandLeft.Clear();
            if (_splitHandRight != null) _splitHandRight.Clear();
            SetBadge(_splitBadgeLeft, null, PushColor);
            SetBadge(_splitBadgeRight, null, PushColor);
        }

        private void RenderDealerConcealed(BlackjackEngine engine)
        {
            IReadOnlyList<Card> cards = engine.DealerHand.Cards;
            bool conceal = cards.Count > 1;
            if (_dealerHandView != null) _dealerHandView.Render(cards, conceal ? 1 : -1);
            if (_dealerHandLabel != null)
            {
                _dealerHandLabel.text = conceal
                    ? $"Dealer  {VisibleValue(cards, 1)} + ?"
                    : "Dealer";
            }
        }

        /// <summary>Draws the first <paramref name="revealed"/> dealer cards face up.</summary>
        private void RenderDealer(BlackjackEngine engine, int revealed)
        {
            IReadOnlyList<Card> cards = engine.DealerHand.Cards;
            int count = Mathf.Clamp(revealed, 0, cards.Count);

            var shown = new List<Card>(count);
            for (int i = 0; i < count; i++) shown.Add(cards[i]);

            if (_dealerHandView != null) _dealerHandView.Render(shown, -1);
            if (_dealerHandLabel != null)
            {
                int value = HandEvaluator.Evaluate(shown).Value;
                _dealerHandLabel.text = value > 21 ? "Dealer  BUST" : $"Dealer  {value}";
            }
        }

        private void RenderPlayer(BlackjackEngine engine)
        {
            IReadOnlyList<Hand> hands = engine.PlayerHands;
            if (hands.Count == 0) return;

            if (hands.Count == 1)
            {
                Hand hand = hands[0];
                if (_playerHandView != null) _playerHandView.Render(hand.Cards);
                if (_splitHandLeft != null) _splitHandLeft.Clear();
                if (_splitHandRight != null) _splitHandRight.Clear();
                SetBadge(_splitBadgeLeft, null, PushColor);
                SetBadge(_splitBadgeRight, null, PushColor);

                if (_playerHandLabel != null)
                {
                    string total = hand.IsBust ? "BUST" : hand.Value.ToString();
                    string soft = !hand.IsBust && hand.IsSoft ? " soft" : "";
                    _playerHandLabel.text = $"You  {total}{soft}";
                }
                return;
            }

            // ---- split: one view per hand, the active one emphasised ----------------
            bool firstSplitFrame = !_splitLayout;
            if (firstSplitFrame)
            {
                // Cards re-deal into their new homes — the split visibly pulls the hand
                // apart rather than teleporting it.
                if (_playerHandView != null) _playerHandView.Clear();
                _splitLayout = true;
            }

            int activeIndex = IndexOfHand(engine, engine.ActiveHand);
            RenderSplitHand(_splitHandLeft, _splitBadgeLeft, hands[0], engine, 0, activeIndex, firstSplitFrame);
            RenderSplitHand(_splitHandRight, _splitBadgeRight, hands[1], engine, 1, activeIndex, firstSplitFrame);

            if (_playerHandLabel != null)
            {
                _playerHandLabel.text = engine.Phase == RoundPhase.PlayerTurn
                    ? $"Playing hand {activeIndex + 1} of {hands.Count}"
                    : "Split hands";
            }
        }

        private void RenderSplitHand(HandView view, TMP_Text badge, Hand hand,
            BlackjackEngine engine, int index, int activeIndex, bool instantEmphasis)
        {
            if (view != null)
            {
                view.Render(hand.Cards);
                bool active = engine.Phase != RoundPhase.PlayerTurn || index == activeIndex;
                view.SetEmphasis(active, instantEmphasis);
            }

            if (badge == null) return;

            // Until results exist the badge carries the running total and stake; the
            // settle sequence swaps in WIN/LOSE per hand.
            HandResult? result = FindResult(hand);
            if (result.HasValue && _outcomeShown)
            {
                ApplyResultBadge(badge, result.Value);
            }
            else
            {
                string total = hand.IsBust ? "BUST" : hand.Value.ToString();
                SetBadge(badge, $"{total}  ·  {hand.Bet:N0}", hand.IsBust ? LoseColor : PushColor);
            }
        }

        private HandResult? FindResult(Hand hand)
        {
            if (_lastResults == null) return null;
            foreach (HandResult r in _lastResults)
                if (ReferenceEquals(r.Hand, hand)) return r;
            return null;
        }

        private void ApplyResultBadge(TMP_Text badge, HandResult result)
        {
            switch (result.Outcome)
            {
                case HandOutcome.PlayerBlackjack:
                    SetBadge(badge, $"BLACKJACK +{result.NetChips:N0}", BlackjackColor); break;
                case HandOutcome.PlayerWin:
                    SetBadge(badge, $"WIN +{result.NetChips:N0}", WinColor); break;
                case HandOutcome.Push:
                    SetBadge(badge, "PUSH", PushColor); break;
                case HandOutcome.PlayerBust:
                    SetBadge(badge, "BUST", LoseColor); break;
                case HandOutcome.Surrendered:
                    SetBadge(badge, "SURRENDERED", PushColor); break;
                default:
                    SetBadge(badge, "LOSE", LoseColor); break;
            }
        }

        private static void SetBadge(TMP_Text badge, string text, Color tint)
        {
            if (badge == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            // The badge's pill frame is its parent; toggling that hides both together.
            if (badge.transform.parent != null)
                badge.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;
            badge.text = text;
            badge.color = tint;
        }

        private static int IndexOfHand(BlackjackEngine engine, Hand hand)
        {
            if (hand == null) return 0;
            for (int i = 0; i < engine.PlayerHands.Count; i++)
                if (ReferenceEquals(engine.PlayerHands[i], hand)) return i;
            return 0;
        }

        /// <summary>Total of just the first <paramref name="count"/> cards.</summary>
        private static int VisibleValue(IReadOnlyList<Card> cards, int count)
        {
            var visible = new List<Card>(count);
            for (int i = 0; i < count && i < cards.Count; i++) visible.Add(cards[i]);
            return HandEvaluator.Evaluate(visible).Value;
        }

        // =====================================================================
        //  Settle sequence
        // =====================================================================

        /// <summary>
        /// The round's third act, in order: the last player card lands, the hole card
        /// turns, the dealer draws one card at a time, the verdict slams in, the chips
        /// move, and only then does the bet row return with the stake re-placed.
        /// </summary>
        private IEnumerator SettleSequence(BlackjackEngine engine)
        {
            // Let the in-flight cards (a double's third card, the split re-deal) land.
            yield return WaitForCardViews();

            // Turn the hole card.
            RenderDealer(engine, Mathf.Min(2, engine.DealerHand.Cards.Count));
            yield return WaitForCardViews();
            yield return new WaitForSeconds(_revealPause);

            // Draw the rest, one by one.
            for (int reveal = 3; reveal <= engine.DealerHand.Cards.Count; reveal++)
            {
                RenderDealer(engine, reveal);
                yield return WaitForCardViews();
                yield return new WaitForSeconds(_drawPause);
            }

            _outcomeShown = true;
            ShowOutcome(engine);
            RefreshBalance();

            // Re-render split hands so the result badges replace the running totals.
            if (engine.PlayerHands.Count > 1) RenderPlayer(engine);

            yield return new WaitForSeconds(0.85f);

            _settling = false;
            PrefillRebet();
            RefreshBetControls();
            ShowRow(_betRow, true);
            ShowRow(_actionRow, false);
        }

        private IEnumerator WaitForCardViews()
        {
            while ((_dealerHandView != null && _dealerHandView.IsAnimating) ||
                   (_playerHandView != null && _playerHandView.IsAnimating) ||
                   (_splitHandLeft != null && _splitHandLeft.IsAnimating) ||
                   (_splitHandRight != null && _splitHandRight.IsAnimating))
            {
                yield return null;
            }
        }

        private void ShowOutcome(BlackjackEngine engine)
        {
            if (_outcomeLabel == null) return;

            string text;
            Color tint;
            float shake;

            if (_lastResults != null && _lastResults.Count == 1)
            {
                HandResult r = _lastResults[0];
                switch (r.Outcome)
                {
                    case HandOutcome.PlayerBlackjack:
                        text = $"BLACKJACK!  +{r.NetChips:N0}"; tint = BlackjackColor; shake = 1.4f; break;
                    case HandOutcome.PlayerWin:
                        text = engine.DealerHand.IsBust
                            ? $"DEALER BUSTS  +{r.NetChips:N0}"
                            : $"YOU WIN  +{r.NetChips:N0}";
                        tint = WinColor; shake = 0.7f; break;
                    case HandOutcome.Push:
                        text = "PUSH — BET RETURNED"; tint = PushColor; shake = 0f; break;
                    case HandOutcome.PlayerBust:
                        text = "BUST"; tint = LoseColor; shake = 1f; break;
                    case HandOutcome.Surrendered:
                        text = "SURRENDERED"; tint = PushColor; shake = 0f; break;
                    default:
                        text = "DEALER WINS"; tint = LoseColor; shake = 0.4f; break;
                }
            }
            else if (_lastResults != null)
            {
                long net = 0;
                bool anyBlackjack = false;
                foreach (HandResult r in _lastResults)
                {
                    net += r.NetChips;
                    if (r.Outcome == HandOutcome.PlayerBlackjack) anyBlackjack = true;
                }

                if (net > 0) { text = $"YOU WIN  +{net:N0}"; tint = anyBlackjack ? BlackjackColor : WinColor; shake = 0.8f; }
                else if (net < 0) { text = "DEALER WINS"; tint = LoseColor; shake = 0.4f; }
                else { text = "EVEN — PUSH"; tint = PushColor; shake = 0f; }
            }
            else
            {
                // Results event never arrived (shouldn't happen); stay silent over lying.
                text = ""; tint = PushColor; shake = 0f;
            }

            _outcomeLabel.text = text;
            if (!string.IsNullOrEmpty(text) && _outcomePunch != null)
                _outcomePunch.Play(tint, Mathf.Max(0.35f, shake));
            if (_shake != null && shake > 0f) _shake.Shake(shake);

            // Any stake coming home sends the chip back to the pill; a clean loss sweeps
            // it to the dealer.
            if (_betChip != null && _lastResults != null)
            {
                bool anyReturned = false;
                foreach (HandResult r in _lastResults)
                {
                    if (r.Outcome == HandOutcome.PlayerWin || r.Outcome == HandOutcome.PlayerBlackjack ||
                        r.Outcome == HandOutcome.Push || r.Outcome == HandOutcome.Surrendered)
                        anyReturned = true;
                }
                _betChip.Settle(anyReturned);
            }
        }

        // =====================================================================
        //  Plumbing
        // =====================================================================

        /// <summary>
        /// Converts another rect's position into the bet chip's coordinate space, so
        /// flights start exactly where the tapped control sits on this device.
        /// </summary>
        private Vector2 ToChipSpace(RectTransform source)
        {
            if (_betChip == null || source == null) return Vector2.zero;
            var parent = _betChip.transform.parent as RectTransform;
            if (parent == null) return Vector2.zero;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, source.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
            return local;
        }

        /// <summary>
        /// Shows or hides a control row.
        ///
        /// The row is deactivated outright, not just faded via the CanvasGroup: TMP draws
        /// its drop-shadow underlay in the shader, and that underlay ignores CanvasGroup
        /// alpha — so a "hidden" row still ghosted its labels through the visible one.
        /// The CanvasGroup is kept for interactivity and for fading later.
        /// </summary>
        private static void ShowRow(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (group.gameObject.activeSelf != visible) group.gameObject.SetActive(visible);
        }

        private void SetActionsInteractable(bool value)
        {
            if (_hitButton != null) _hitButton.interactable = value;
            if (_standButton != null) _standButton.interactable = value;
            if (_doubleButton != null) _doubleButton.interactable = value;
            if (_splitButton != null) _splitButton.interactable = value;
        }
    }
}
