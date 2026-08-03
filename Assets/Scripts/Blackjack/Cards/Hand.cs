using System.Collections.Generic;

namespace BlackjackGame.Blackjack.Cards
{
    /// <summary>
    /// A collection of cards held by the player or dealer. Value calculation is
    /// delegated to <see cref="HandEvaluator"/> to keep this a plain container.
    /// </summary>
    public sealed class Hand
    {
        private readonly List<Card> _cards = new List<Card>();

        public IReadOnlyList<Card> Cards => _cards;
        public int Bet { get; set; }

        /// <summary>Set true when a hand was created by a split; used by rule sets.</summary>
        public bool IsSplitHand { get; set; }

        /// <summary>Set true once the player has doubled down on this hand.</summary>
        public bool HasDoubled { get; set; }

        public void Add(Card card) => _cards.Add(card);
        public void Clear() { _cards.Clear(); Bet = 0; HasDoubled = false; IsSplitHand = false; }

        public int Value => HandEvaluator.Evaluate(_cards).Value;
        public bool IsSoft => HandEvaluator.Evaluate(_cards).IsSoft;
        public bool IsBust => Value > 21;
        public bool IsBlackjack => HandEvaluator.IsBlackjack(_cards);

        /// <summary>Splittable when it holds exactly two cards of equal blackjack value.</summary>
        public bool CanSplit =>
            _cards.Count == 2 && _cards[0].BlackjackValue == _cards[1].BlackjackValue;

        /// <summary>Removes and returns the last card — used when splitting a pair.</summary>
        public Card RemoveLast()
        {
            var last = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            return last;
        }
    }
}
