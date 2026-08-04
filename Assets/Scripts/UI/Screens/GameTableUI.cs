using System.Collections.Generic;
using BlackjackGame.Blackjack;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Core;
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

        private GameManager _game;

        private void Start()
        {
            _game = GameManager.Instance;

            if (_dealButton != null) _dealButton.onClick.AddListener(OnDeal);
            if (_hitButton != null) _hitButton.onClick.AddListener(() => { _game.Hit(); Refresh(); });
            if (_standButton != null) _standButton.onClick.AddListener(() => { _game.Stand(); Refresh(); });
            if (_doubleButton != null) _doubleButton.onClick.AddListener(() => { _game.DoubleDown(); Refresh(); });
            if (_splitButton != null) _splitButton.onClick.AddListener(() => { _game.Split(); Refresh(); });
            if (_backButton != null) _backButton.onClick.AddListener(() => _game.GoToScene(SceneNames.MainMenu));

            if (_game != null) _game.OnRoundComplete += _ => Refresh();

            Refresh();
        }

        private void OnDeal()
        {
            int bet = 100;
            if (_betInput != null && int.TryParse(_betInput.text, out var parsed)) bet = parsed;
            _game.PlaceBetAndDeal(bet);
            if (_outcomeLabel != null) _outcomeLabel.text = "";
            Refresh();
        }

        private void Refresh()
        {
            // Just the number — it sits inside the coin pill, which carries the meaning.
            if (_balanceLabel != null) _balanceLabel.text = $"{_game.Balance:N0}";

            var engine = _game.Engine;
            if (engine == null)
            {
                SetActionsInteractable(false);
                if (_dealButton != null) _dealButton.interactable = true;
                if (_dealerHandView != null) _dealerHandView.Clear();
                if (_playerHandView != null) _playerHandView.Clear();
                if (_dealerHandLabel != null) _dealerHandLabel.text = "Dealer";
                if (_playerHandLabel != null) _playerHandLabel.text = "Place your bet";
                ShowRow(_betRow, true);
                ShowRow(_actionRow, false);
                return;
            }

            RenderHands(engine);

            bool playing = engine.Phase == RoundPhase.PlayerTurn;
            if (_hitButton != null) _hitButton.interactable = engine.CanHit;
            if (_standButton != null) _standButton.interactable = engine.CanStand;
            if (_doubleButton != null) _doubleButton.interactable = engine.CanDouble;
            if (_splitButton != null) _splitButton.interactable = engine.CanSplit;
            if (_dealButton != null) _dealButton.interactable = !playing;

            // Betting and playing are mutually exclusive, so the two control rows share
            // the same band on the rail — the mockup only ever shows one of them.
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

            if (engine.PlayerHands.Count == 1)
            {
                Hand hand = engine.PlayerHands[0];
                _outcomeLabel.text =
                    hand.IsBust ? "Bust" :
                    engine.DealerHand.IsBust ? "Dealer busts - you win" :
                    hand.IsBlackjack && !engine.DealerHand.IsBlackjack ? "Blackjack!" :
                    hand.Value > engine.DealerHand.Value ? "You win" :
                    hand.Value < engine.DealerHand.Value ? "Dealer wins" : "Push";
                return;
            }

            var parts = new List<string>(engine.PlayerHands.Count);
            for (int i = 0; i < engine.PlayerHands.Count; i++)
            {
                Hand hand = engine.PlayerHands[i];
                parts.Add($"H{i + 1} {(hand.IsBust ? "bust" : hand.Value.ToString())}");
            }
            _outcomeLabel.text = string.Join("   ", parts);
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
