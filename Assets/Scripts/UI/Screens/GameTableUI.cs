using System.Collections.Generic;
using System.Text;
using BlackjackGame.Blackjack;
using BlackjackGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Screens
{
    /// <summary>
    /// Basic game-table layout and controls. Renders hands as text placeholders (swap for
    /// card sprites/prefabs later) and enables/disables action buttons based on the
    /// engine's legal-move flags. UI stays declarative: it reflects engine state, never
    /// duplicates rules.
    /// </summary>
    public sealed class GameTableUI : MonoBehaviour
    {
        [Header("Bet Controls")]
        [SerializeField] private InputField _betInput;
        [SerializeField] private Button _dealButton;

        [Header("Action Buttons")]
        [SerializeField] private Button _hitButton;
        [SerializeField] private Button _standButton;
        [SerializeField] private Button _doubleButton;
        [SerializeField] private Button _splitButton;

        [Header("Display")]
        [SerializeField] private Text _dealerHandLabel;
        [SerializeField] private Text _playerHandLabel;
        [SerializeField] private Text _outcomeLabel;
        [SerializeField] private Text _balanceLabel;

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
            if (_balanceLabel != null) _balanceLabel.text = $"Chips: {_game.Balance:N0}";

            var engine = _game.Engine;
            if (engine == null)
            {
                SetActionsInteractable(false);
                if (_dealButton != null) _dealButton.interactable = true;
                return;
            }

            RenderHands(engine);

            bool playing = engine.Phase == RoundPhase.PlayerTurn;
            if (_hitButton != null) _hitButton.interactable = engine.CanHit;
            if (_standButton != null) _standButton.interactable = engine.CanStand;
            if (_doubleButton != null) _doubleButton.interactable = engine.CanDouble;
            if (_splitButton != null) _splitButton.interactable = engine.CanSplit;
            if (_dealButton != null) _dealButton.interactable = !playing;

            if (engine.Phase == RoundPhase.Settled) ShowOutcome(engine);
        }

        private void RenderHands(BlackjackEngine engine)
        {
            if (_dealerHandLabel != null)
                _dealerHandLabel.text = $"Dealer ({engine.DealerHand.Value}): {Describe(engine.DealerHand.Cards)}";

            if (_playerHandLabel != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < engine.PlayerHands.Count; i++)
                {
                    var h = engine.PlayerHands[i];
                    string marker = (h == engine.ActiveHand) ? "▶ " : "  ";
                    sb.AppendLine($"{marker}Hand {i + 1} ({h.Value}): {Describe(h.Cards)}  [bet {h.Bet}]");
                }
                _playerHandLabel.text = sb.ToString();
            }
        }

        private static string Describe(IReadOnlyList<Blackjack.Cards.Card> cards)
        {
            var sb = new StringBuilder();
            foreach (var c in cards) sb.Append(c.ShortCode).Append(' ');
            return sb.ToString().Trim();
        }

        private void ShowOutcome(BlackjackEngine engine)
        {
            if (_outcomeLabel == null) return;
            var sb = new StringBuilder();
            foreach (var h in engine.PlayerHands)
            {
                // Recompute a friendly label from value vs dealer for display only.
                sb.Append(h.IsBust ? "Bust  " : $"{h.Value}  ");
            }
            _outcomeLabel.text = $"Round over — {sb}".Trim();
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
