using BlackjackGame.Blackjack.Cards;

namespace BlackjackGame.Blackjack.Rules
{
    /// <summary>
    /// European "no-hole-card" rules: dealer draws the second card only after players
    /// act, does not peek, stands on soft 17, doubling restricted to hard 9-11,
    /// and splitting is limited.
    /// </summary>
    public sealed class EuropeanRules : IRuleSet
    {
        public string DisplayName => "European";
        public int DeckCount => 6;
        public float BlackjackPayout => 1.5f;
        public bool DealerHitsSoft17 => false;
        public bool DealerPeeksForBlackjack => false;
        public int MaxSplits => 2;
        public bool DoubleAfterSplitAllowed => false;
        public bool RestrictDoubleTo9To11 => true;
        public bool SurrenderAllowed => false;

        public bool CanDouble(Hand hand)
        {
            if (hand.Cards.Count != 2) return false;
            if (hand.IsSplitHand && !DoubleAfterSplitAllowed) return false;

            if (RestrictDoubleTo9To11)
            {
                // Only hard 9, 10 or 11 may double.
                int v = hand.Value;
                return !hand.IsSoft && v >= 9 && v <= 11;
            }
            return true;
        }

        public bool CanSplit(Hand hand, int currentHandCount)
        {
            return hand.CanSplit && currentHandCount < MaxSplits;
        }
    }
}
