using System.Collections.Generic;
using BlackjackGame.Blackjack.Cards;

namespace BlackjackGame.Blackjack
{
    /// <summary>Result of scoring a hand.</summary>
    public readonly struct HandScore
    {
        public readonly int Value;
        /// <summary>True when an Ace is still counted as 11 (a "soft" total).</summary>
        public readonly bool IsSoft;

        public HandScore(int value, bool isSoft)
        {
            Value = value;
            IsSoft = isSoft;
        }
    }

    /// <summary>
    /// Stateless scoring helper. Kept separate from <see cref="Hand"/> so the same
    /// logic can score dealer hands, split hands, or hypothetical hands in AI look-ahead.
    /// </summary>
    public static class HandEvaluator
    {
        public const int BlackjackTarget = 21;

        /// <summary>
        /// Computes the best (highest not-busting) total, demoting Aces from 11 to 1
        /// as needed.
        /// </summary>
        public static HandScore Evaluate(IReadOnlyList<Card> cards)
        {
            int total = 0;
            int aces = 0;

            foreach (var card in cards)
            {
                total += card.BlackjackValue; // Ace initially counts as 11
                if (card.IsAce) aces++;
            }

            // Demote Aces from 11 to 1 while we are over the target.
            bool soft = aces > 0;
            while (total > BlackjackTarget && aces > 0)
            {
                total -= 10;
                aces--;
            }

            // Still soft only if an Ace remains valued at 11.
            soft = aces > 0 && total <= BlackjackTarget;
            return new HandScore(total, soft);
        }

        /// <summary>A natural: exactly two cards totalling 21.</summary>
        public static bool IsBlackjack(IReadOnlyList<Card> cards)
        {
            return cards.Count == 2 && Evaluate(cards).Value == BlackjackTarget;
        }
    }
}
