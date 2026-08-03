using System.Collections.Generic;
using BlackjackGame.Utils;

namespace BlackjackGame.Blackjack.Cards
{
    /// <summary>
    /// A shoe of one or more 52-card decks. Uses an injected <see cref="IRandomProvider"/>
    /// so shuffling is deterministic in tests and swappable for a server-seeded RNG later.
    /// </summary>
    public sealed class Deck
    {
        private readonly List<Card> _cards = new List<Card>();
        private readonly IRandomProvider _rng;
        private int _drawIndex;

        /// <summary>Number of 52-card decks composing this shoe.</summary>
        public int DeckCount { get; }

        /// <summary>Cards remaining before a reshuffle is required.</summary>
        public int RemainingCount => _cards.Count - _drawIndex;

        public Deck(int deckCount = 1, IRandomProvider rng = null)
        {
            DeckCount = deckCount < 1 ? 1 : deckCount;
            _rng = rng ?? new SystemRandomProvider();
            Build();
            Shuffle();
        }

        private void Build()
        {
            _cards.Clear();
            _drawIndex = 0;
            for (int d = 0; d < DeckCount; d++)
            {
                foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
                {
                    foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
                    {
                        _cards.Add(new Card(suit, rank));
                    }
                }
            }
        }

        /// <summary>Fisher-Yates shuffle. Rebuilds the shoe if it was partially drawn.</summary>
        public void Shuffle()
        {
            if (_drawIndex > 0)
            {
                Build();
            }

            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }

            _drawIndex = 0;
        }

        /// <summary>Draw the top card. Automatically reshuffles when the shoe is exhausted.</summary>
        public Card Draw()
        {
            if (RemainingCount <= 0)
            {
                Shuffle();
            }
            return _cards[_drawIndex++];
        }

        /// <summary>
        /// True when the penetration point has been reached and the shoe should be
        /// reshuffled between rounds (cut-card simulation).
        /// </summary>
        public bool NeedsReshuffle(float penetration = 0.75f)
        {
            return _drawIndex >= _cards.Count * penetration;
        }
    }
}
