using System;

namespace BlackjackGame.Blackjack.Cards
{
    /// <summary>The four French-deck suits.</summary>
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    /// <summary>
    /// Card rank. The integer value is the "pip" value; face cards resolve to 10
    /// and the Ace is special-cased (1 or 11) by the <see cref="HandEvaluator"/>.
    /// </summary>
    public enum Rank
    {
        Ace = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13
    }

    /// <summary>
    /// Immutable playing card. Pure data — no Unity dependency so it can be
    /// unit-tested and reused on the backend if needed.
    /// </summary>
    [Serializable]
    public readonly struct Card : IEquatable<Card>
    {
        public readonly Suit Suit;
        public readonly Rank Rank;

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>Blackjack pip value: face cards count as 10, Ace as 11 (soft).</summary>
        public int BlackjackValue => Rank switch
        {
            Rank.Ace => 11,
            Rank.Jack or Rank.Queen or Rank.King => 10,
            _ => (int)Rank
        };

        public bool IsAce => Rank == Rank.Ace;

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => ((int)Suit * 100) + (int)Rank;

        /// <summary>Short human-readable code, e.g. "AS" (Ace of Spades), "10H".</summary>
        public string ShortCode
        {
            get
            {
                string r = Rank switch
                {
                    Rank.Ace => "A",
                    Rank.Ten => "10",
                    Rank.Jack => "J",
                    Rank.Queen => "Q",
                    Rank.King => "K",
                    _ => ((int)Rank).ToString()
                };
                return r + Suit.ToString()[0];
            }
        }

        public override string ToString() => $"{Rank} of {Suit}";
    }
}
