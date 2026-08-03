using BlackjackGame.Blackjack.Cards;

namespace BlackjackGame.Blackjack.Rules
{
    /// <summary>
    /// Strategy interface describing a blackjack variant. New variants (Vegas, Atlantic
    /// City, Spanish 21, …) are added by implementing this — the engine never changes.
    /// </summary>
    public interface IRuleSet
    {
        string DisplayName { get; }

        /// <summary>Number of 52-card decks in the shoe.</summary>
        int DeckCount { get; }

        /// <summary>Payout multiplier for a natural blackjack (e.g. 1.5 for 3:2).</summary>
        float BlackjackPayout { get; }

        /// <summary>Whether the dealer draws on a soft 17.</summary>
        bool DealerHitsSoft17 { get; }

        /// <summary>Whether the dealer takes a face-down hole card at deal time (US) vs. no-hole-card (European).</summary>
        bool DealerPeeksForBlackjack { get; }

        /// <summary>Max number of hands a player may split to (1 = no splitting).</summary>
        int MaxSplits { get; }

        /// <summary>Whether the player may double after splitting.</summary>
        bool DoubleAfterSplitAllowed { get; }

        /// <summary>Whether double down is restricted to hard 9/10/11 (true) or any two cards (false).</summary>
        bool RestrictDoubleTo9To11 { get; }

        /// <summary>Whether surrender is offered.</summary>
        bool SurrenderAllowed { get; }

        /// <summary>Rule-specific check for whether a hand may double right now.</summary>
        bool CanDouble(Hand hand);

        /// <summary>Rule-specific check for whether a hand may split right now.</summary>
        bool CanSplit(Hand hand, int currentHandCount);
    }
}
