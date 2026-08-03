using System;
using System.Collections.Generic;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Blackjack.Rules;
using BlackjackGame.Utils;

namespace BlackjackGame.Blackjack
{
    public enum RoundPhase
    {
        Idle,
        PlayerTurn,
        DealerTurn,
        Settled
    }

    public enum HandOutcome
    {
        Pending,
        PlayerBlackjack,
        PlayerWin,
        DealerWin,
        Push,
        PlayerBust,
        Surrendered
    }

    /// <summary>
    /// The heart of the game: a rules-agnostic state machine that deals, exposes legal
    /// player actions, drives the dealer, and settles bets. UI and networking layers sit
    /// on top of this and never touch cards directly.
    ///
    /// The engine is intentionally free of Unity types so it can be unit-tested headless
    /// and, later, mirrored on an authoritative server.
    /// </summary>
    public sealed class BlackjackEngine
    {
        private readonly IRuleSet _rules;
        private readonly DealerAI _dealer;
        private readonly Deck _deck;

        private readonly List<Hand> _playerHands = new List<Hand>();
        private int _activeHandIndex;

        public Hand DealerHand { get; private set; } = new Hand();
        public IReadOnlyList<Hand> PlayerHands => _playerHands;
        public RoundPhase Phase { get; private set; } = RoundPhase.Idle;
        public IRuleSet Rules => _rules;

        /// <summary>The hand the player is currently acting on (relevant when split).</summary>
        public Hand ActiveHand =>
            _activeHandIndex < _playerHands.Count ? _playerHands[_activeHandIndex] : null;

        // ---- Events (UI subscribes to these instead of polling) ----
        public event Action<Card, bool> OnCardDealt;          // card, isDealerCard
        public event Action<RoundPhase> OnPhaseChanged;
        public event Action<IReadOnlyList<HandResult>> OnRoundSettled;

        public BlackjackEngine(IRuleSet rules, IRandomProvider rng = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _dealer = new DealerAI(_rules);
            _deck = new Deck(_rules.DeckCount, rng);
        }

        /// <summary>Starts a new round with the given bet on the first hand.</summary>
        public void StartRound(int bet)
        {
            if (_deck.NeedsReshuffle()) _deck.Shuffle();

            _playerHands.Clear();
            DealerHand = new Hand();
            _activeHandIndex = 0;

            var player = new Hand { Bet = bet };
            _playerHands.Add(player);

            // Standard deal order: player, dealer, player, dealer(hole).
            Deal(player, isDealer: false);
            Deal(DealerHand, isDealer: true);
            Deal(player, isDealer: false);
            if (_rules.DealerPeeksForBlackjack)
            {
                Deal(DealerHand, isDealer: true); // hole card dealt immediately (US style)
            }

            SetPhase(RoundPhase.PlayerTurn);

            // Immediate resolution on naturals.
            if (player.IsBlackjack || (_rules.DealerPeeksForBlackjack && DealerHand.IsBlackjack))
            {
                AdvanceToDealer();
            }
        }

        // ---------- Player actions ----------

        public bool CanHit => Phase == RoundPhase.PlayerTurn && ActiveHand != null && !ActiveHand.IsBust;
        public bool CanStand => Phase == RoundPhase.PlayerTurn && ActiveHand != null;
        public bool CanDouble => Phase == RoundPhase.PlayerTurn && ActiveHand != null && _rules.CanDouble(ActiveHand);
        public bool CanSplit => Phase == RoundPhase.PlayerTurn && ActiveHand != null && _rules.CanSplit(ActiveHand, _playerHands.Count);
        public bool CanSurrender => Phase == RoundPhase.PlayerTurn && _rules.SurrenderAllowed
                                    && ActiveHand != null && ActiveHand.Cards.Count == 2 && !ActiveHand.IsSplitHand;

        public void Hit()
        {
            if (!CanHit) return;
            Deal(ActiveHand, isDealer: false);
            if (ActiveHand.IsBust) NextHandOrDealer();
        }

        public void Stand()
        {
            if (!CanStand) return;
            NextHandOrDealer();
        }

        /// <summary>Doubles the bet, draws exactly one card, then ends the hand.</summary>
        public void DoubleDown()
        {
            if (!CanDouble) return;
            ActiveHand.Bet *= 2;
            ActiveHand.HasDoubled = true;
            Deal(ActiveHand, isDealer: false);
            NextHandOrDealer();
        }

        /// <summary>
        /// Splits the active pair into two hands. The second card seeds a new hand which
        /// each receive one fresh card.
        /// </summary>
        public void Split()
        {
            if (!CanSplit) return;

            var source = ActiveHand;
            var moved = source.RemoveLast();

            var newHand = new Hand { Bet = source.Bet, IsSplitHand = true };
            source.IsSplitHand = true;
            newHand.Add(moved);
            _playerHands.Insert(_activeHandIndex + 1, newHand);

            // Deal one card to each split hand.
            Deal(source, isDealer: false);
            Deal(newHand, isDealer: false);
        }

        public void Surrender()
        {
            if (!CanSurrender) return;
            // Mark and settle immediately for this simple single-hand case.
            _playerHands[_activeHandIndex].Bet = _playerHands[_activeHandIndex].Bet; // bet retained for half-refund calc
            Settle(surrenderedIndex: _activeHandIndex);
        }

        // ---------- Internal flow ----------

        private void Deal(Hand hand, bool isDealer)
        {
            var card = _deck.Draw();
            hand.Add(card);
            OnCardDealt?.Invoke(card, isDealer);
        }

        private void NextHandOrDealer()
        {
            if (_activeHandIndex < _playerHands.Count - 1)
            {
                _activeHandIndex++;
            }
            else
            {
                AdvanceToDealer();
            }
        }

        private void AdvanceToDealer()
        {
            SetPhase(RoundPhase.DealerTurn);

            // No-hole-card variants deal the dealer's second card now.
            if (!_rules.DealerPeeksForBlackjack && DealerHand.Cards.Count == 1)
            {
                Deal(DealerHand, isDealer: true);
            }

            // Dealer only draws if at least one player hand is still live.
            if (AnyPlayerHandLive())
            {
                _dealer.PlayOut(DealerHand, _deck);
            }

            Settle();
        }

        private bool AnyPlayerHandLive()
        {
            foreach (var h in _playerHands)
                if (!h.IsBust) return true;
            return false;
        }

        private void Settle(int surrenderedIndex = -1)
        {
            var results = new List<HandResult>(_playerHands.Count);
            bool dealerBj = DealerHand.IsBlackjack;
            bool dealerBust = DealerHand.IsBust;
            int dealerValue = DealerHand.Value;

            for (int i = 0; i < _playerHands.Count; i++)
            {
                var hand = _playerHands[i];
                HandOutcome outcome;
                float payoutMultiplier; // net multiplier applied to the bet

                if (i == surrenderedIndex)
                {
                    outcome = HandOutcome.Surrendered;
                    payoutMultiplier = -0.5f;
                }
                else if (hand.IsBust)
                {
                    outcome = HandOutcome.PlayerBust;
                    payoutMultiplier = -1f;
                }
                else if (hand.IsBlackjack && !dealerBj)
                {
                    outcome = HandOutcome.PlayerBlackjack;
                    payoutMultiplier = _rules.BlackjackPayout;
                }
                else if (dealerBj && !hand.IsBlackjack)
                {
                    outcome = HandOutcome.DealerWin;
                    payoutMultiplier = -1f;
                }
                else if (dealerBj && hand.IsBlackjack)
                {
                    outcome = HandOutcome.Push;
                    payoutMultiplier = 0f;
                }
                else if (dealerBust || hand.Value > dealerValue)
                {
                    outcome = HandOutcome.PlayerWin;
                    payoutMultiplier = 1f;
                }
                else if (hand.Value < dealerValue)
                {
                    outcome = HandOutcome.DealerWin;
                    payoutMultiplier = -1f;
                }
                else
                {
                    outcome = HandOutcome.Push;
                    payoutMultiplier = 0f;
                }

                results.Add(new HandResult(hand, outcome, Round(hand.Bet * payoutMultiplier)));
            }

            SetPhase(RoundPhase.Settled);
            OnRoundSettled?.Invoke(results);
        }

        private static int Round(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        private void SetPhase(RoundPhase phase)
        {
            Phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }
    }

    /// <summary>The settlement outcome for a single hand, including the net chip delta.</summary>
    public readonly struct HandResult
    {
        public readonly Hand Hand;
        public readonly HandOutcome Outcome;
        /// <summary>Net chips won (positive) or lost (negative) for this hand.</summary>
        public readonly int NetChips;

        public HandResult(Hand hand, HandOutcome outcome, int netChips)
        {
            Hand = hand;
            Outcome = outcome;
            NetChips = netChips;
        }
    }
}
