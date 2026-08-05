using System;
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
    /// Game-table layout and controls. Hands are drawn as card sprites via
    /// <see cref="HandView"/>; the labels carry only the totals. Action buttons are
    /// enabled straight from the engine's legal-move flags, so the UI reflects engine
    /// state and never duplicates rules.
    /// </summary>
    public sealed class GameTableUI : MonoBehaviour
    {
        [Header("Bet Controls")]
        [SerializeField] private TMP_InputField _betInput;
        [SerializeField] private Button _dealButton;
        [SerializeField] private Button _betMinusButton;
        [SerializeField] private Button _betPlusButton;
        [Tooltip("How much one tap of - or + moves the stake.")]
        [SerializeField] private int _betStep = 100;

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
        [SerializeField] private HandView _playerHandView;

        [Header("Display")]
        [SerializeField] private TMP_Text _dealerHandLabel;
        [SerializeField] private TMP_Text _playerHandLabel;
        [SerializeField] private TMP_Text _outcomeLabel;
        [SerializeField] private TMP_Text _balanceLabel;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        [Header("Quick Actions (top bar)")]
        [SerializeField] private Button _giftButton;
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
        [Tooltip("The stake on the felt — flies out on a bet, back on a win.")]
        [SerializeField] private BetChipView _betChip;

        /// <summary>Outcome tints. Gold for a blackjack, green win, red loss, grey push.</summary>
        private static readonly Color WinColor = new Color(0.42f, 1f, 0.55f);
        private static readonly Color LoseColor = new Color(1f, 0.38f, 0.38f);
        private static readonly Color BlackjackColor = new Color(1f, 0.86f, 0.35f);
        private static readonly Color PushColor = new Color(0.85f, 0.85f, 0.85f);

        private GameManager _game;

        /// <summary>
        /// The engine instance the views were last drawn for. GameManager builds a fresh
        /// engine per round, so a change here means "new round" — which is when the hands
        /// need clearing so the next Render deals the cards in rather than snapping them.
        /// </summary>
        private BlackjackEngine _renderedEngine;

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
            if (_backButton != null) _backButton.onClick.AddListener(() => _game.GoToScene(SceneNames.MainMenu));

            if (_betMinusButton != null) _betMinusButton.onClick.AddListener(() => AdjustBet(-_betStep));
            if (_betPlusButton != null) _betPlusButton.onClick.AddListener(() => AdjustBet(_betStep));

            if (_giftButton != null) _giftButton.onClick.AddListener(ClaimGift);
            if (_settingsButton != null && _settingsPanel != null)
                _settingsButton.onClick.AddListener(_settingsPanel.Show);
            if (_trophyButton != null && _statsPanel != null)
                _trophyButton.onClick.AddListener(_statsPanel.Show);

            if (_game != null) _game.OnRoundComplete += _ => Refresh();

            Refresh();
        }

        /// <summary>
        /// Same claim flow as the Main Menu's daily-reward button, surfaced here as a
        /// quick action. Result is shown in the outcome label — it's free between rounds
        /// and gets overwritten by the next round's result either way.
        /// </summary>
        private void ClaimGift()
        {
            if (!AppManager.Exists || _outcomeLabel == null) return;

            DailyRewardResult result = AppManager.Instance.Rewards.TryClaim(DateTime.UtcNow);
            _outcomeLabel.text = result.Success
                ? $"+{result.ChipsAwarded:N0} chips! Streak: {result.NewStreak}"
                : $"Next reward in {result.TimeUntilNext.Hours}h {result.TimeUntilNext.Minutes}m";
        }

        /// <summary>The stake currently typed in the field, or 100 if it is empty/unparseable.</summary>
        private int CurrentBet =>
            _betInput != null && int.TryParse(_betInput.text, out int parsed) ? parsed : 100;

        /// <summary>Lowest legal stake, from config.</summary>
        private int MinBet =>
            AppManager.Exists && AppManager.Instance.GameConfig != null
                ? AppManager.Instance.GameConfig.MinBet
                : 1;

        /// <summary>
        /// Highest legal stake: the configured ceiling, but never more than the player
        /// actually holds — offering a bet that will be refused is worse than not offering it.
        /// </summary>
        private int MaxBet
        {
            get
            {
                int ceiling = AppManager.Exists && AppManager.Instance.GameConfig != null
                    ? AppManager.Instance.GameConfig.MaxBet
                    : int.MaxValue;
                long affordable = _game != null ? _game.Balance : 0;
                return (int)Mathf.Min(ceiling, Mathf.Max(MinBet, affordable));
            }
        }

        /// <summary>Nudges the stake by one step, clamped to what is legal and affordable.</summary>
        private void AdjustBet(int delta)
        {
            if (_betInput == null) return;
            int next = Mathf.Clamp(CurrentBet + delta, MinBet, MaxBet);
            _betInput.text = next.ToString();
            RefreshBetControls();
        }

        /// <summary>Greys out a stepper once the stake is against its limit.</summary>
        private void RefreshBetControls()
        {
            int bet = CurrentBet;
            if (_betMinusButton != null) _betMinusButton.interactable = bet > MinBet;
            if (_betPlusButton != null) _betPlusButton.interactable = bet < MaxBet;
        }

        private void OnDeal()
        {
            int bet = 100;
            if (_betInput != null && int.TryParse(_betInput.text, out var parsed)) bet = parsed;

            // Only throw the chip if the bet was actually accepted — an unaffordable bet
            // leaves the balance untouched, so a chip sailing onto the felt would lie.
            bool placed = _game.PlaceBetAndDeal(bet);

            if (_outcomeLabel != null) _outcomeLabel.text = "";
            if (_outcomePunch != null) _outcomePunch.ResetNow();
            _outcomeShown = false;

            if (placed && _betChip != null) _betChip.PlaceBet(bet);
            Refresh();
        }

        private void Refresh()
        {
            // Just the number — it sits inside the coin pill, which carries the meaning.
            // The rollup owns the label's text when present, so don't write both.
            if (_balanceRollup != null) _balanceRollup.SetValue(_game.Balance);
            else if (_balanceLabel != null) _balanceLabel.text = $"{_game.Balance:N0}";

            var engine = _game.Engine;
            if (engine == null)
            {
                SetActionsInteractable(false);
                if (_dealButton != null) _dealButton.interactable = true;
                if (_dealerHandView != null) _dealerHandView.Clear();
                if (_playerHandView != null) _playerHandView.Clear();
                if (_dealerHandLabel != null) _dealerHandLabel.text = "Dealer";
                if (_playerHandLabel != null) _playerHandLabel.text = "Place your bet";
                if (_betChip != null) _betChip.Hide();
                RefreshBetControls();
                ShowRow(_betRow, true);
                ShowRow(_actionRow, false);
                return;
            }

            RenderHands(engine);

            // Keep the chip's number honest: doubling and splitting both change how much
            // is actually at risk, so total it from the hands rather than the opening bet.
            if (_betChip != null)
            {
                long staked = 0;
                foreach (Hand h in engine.PlayerHands) staked += h.Bet;
                if (staked > 0) _betChip.SetAmount(staked);
            }

            bool playing = engine.Phase == RoundPhase.PlayerTurn;
            if (_hitButton != null) _hitButton.interactable = engine.CanHit;
            if (_standButton != null) _standButton.interactable = engine.CanStand;
            if (_doubleButton != null) _doubleButton.interactable = engine.CanDouble;
            if (_splitButton != null) _splitButton.interactable = engine.CanSplit;
            if (_dealButton != null) _dealButton.interactable = !playing;

            // Betting and playing are mutually exclusive, so the two control rows share
            // the same band on the rail — the mockup only ever shows one of them.
            if (!playing) RefreshBetControls();
            ShowRow(_betRow, !playing);
            ShowRow(_actionRow, playing);

            if (engine.Phase == RoundPhase.Settled) ShowOutcome(engine);
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

        private void RenderHands(BlackjackEngine engine)
        {
            if (!ReferenceEquals(engine, _renderedEngine))
            {
                if (_dealerHandView != null) _dealerHandView.Clear();
                if (_playerHandView != null) _playerHandView.Clear();
                _renderedEngine = engine;
            }

            // While the player is still acting the hole card stays face down, and the
            // dealer total shown is only what the player can actually see. Showing the
            // real total here would leak the hidden card.
            bool concealHole = engine.Phase == RoundPhase.PlayerTurn
                               && engine.DealerHand.Cards.Count > 1;

            if (_dealerHandView != null)
                _dealerHandView.Render(engine.DealerHand.Cards, concealHole ? 1 : -1);

            if (_dealerHandLabel != null)
            {
                _dealerHandLabel.text = concealHole
                    ? $"Dealer  {VisibleValue(engine.DealerHand.Cards, 1)} + ?"
                    : $"Dealer  {engine.DealerHand.Value}";
            }

            Hand active = engine.ActiveHand;
            if (active == null && engine.PlayerHands.Count > 0) active = engine.PlayerHands[0];
            if (active == null) return;

            if (_playerHandView != null) _playerHandView.Render(active.Cards);

            if (_playerHandLabel != null)
            {
                string prefix = engine.PlayerHands.Count > 1
                    ? $"Hand {IndexOfHand(engine, active) + 1}/{engine.PlayerHands.Count}  -  "
                    : "";
                string total = active.IsBust ? "BUST" : active.Value.ToString();
                string soft = !active.IsBust && active.IsSoft ? " soft" : "";
                _playerHandLabel.text = $"{prefix}You  {total}{soft}  -  bet {active.Bet:N0}";
            }
        }

        private static int IndexOfHand(BlackjackEngine engine, Hand hand)
        {
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

        private void ShowOutcome(BlackjackEngine engine)
        {
            if (_outcomeLabel == null) return;

            // Only react once per round. Refresh runs on every UI event, and replaying the
            // slam and shake on each of them would strobe the screen.
            bool firstTime = !_outcomeShown;
            _outcomeShown = true;

            if (engine.PlayerHands.Count == 1)
            {
                Hand hand = engine.PlayerHands[0];

                string text;
                Color tint;
                float shake;

                if (hand.IsBust)
                {
                    text = "BUST"; tint = LoseColor; shake = 1f;
                }
                else if (engine.DealerHand.IsBust)
                {
                    text = "DEALER BUSTS — YOU WIN"; tint = WinColor; shake = 0.7f;
                }
                else if (hand.IsBlackjack && !engine.DealerHand.IsBlackjack)
                {
                    text = "BLACKJACK!"; tint = BlackjackColor; shake = 1.4f;
                }
                else if (hand.Value > engine.DealerHand.Value)
                {
                    text = "YOU WIN"; tint = WinColor; shake = 0.55f;
                }
                else if (hand.Value < engine.DealerHand.Value)
                {
                    text = "DEALER WINS"; tint = LoseColor; shake = 0.4f;
                }
                else
                {
                    text = "PUSH"; tint = PushColor; shake = 0f;
                }

                _outcomeLabel.text = text;
                if (firstTime)
                {
                    PlayOutcomeReaction(tint, shake);
                    // Stake comes back on anything that isn't an outright loss.
                    bool keepsStake = !hand.IsBust && (engine.DealerHand.IsBust ||
                                                       hand.Value >= engine.DealerHand.Value);
                    if (_betChip != null) _betChip.Settle(keepsStake);
                }
                return;
            }

            var parts = new List<string>(engine.PlayerHands.Count);
            for (int i = 0; i < engine.PlayerHands.Count; i++)
            {
                Hand hand = engine.PlayerHands[i];
                parts.Add($"H{i + 1} {(hand.IsBust ? "bust" : hand.Value.ToString())}");
            }
            _outcomeLabel.text = string.Join("   ", parts);
            if (firstTime)
            {
                PlayOutcomeReaction(PushColor, 0.5f);
                // Split hands can land either way; treat any surviving hand as keeping
                // the stake so the chip's exit matches the balance moving up.
                bool anySurvived = false;
                foreach (Hand h in engine.PlayerHands)
                    if (!h.IsBust) anySurvived = true;
                if (_betChip != null) _betChip.Settle(anySurvived);
            }
        }

        private void PlayOutcomeReaction(Color tint, float shakeStrength)
        {
            if (_outcomePunch != null) _outcomePunch.Play(tint, Mathf.Max(0.35f, shakeStrength));
            if (_shake != null && shakeStrength > 0f) _shake.Shake(shakeStrength);
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
