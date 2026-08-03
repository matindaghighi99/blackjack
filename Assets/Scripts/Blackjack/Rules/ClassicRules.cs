using BlackjackGame.Blackjack.Cards;

namespace BlackjackGame.Blackjack.Rules
{
    /// <summary>
    /// Classic US / Vegas-style rules: dealer takes a hole card and peeks for blackjack,
    /// 6-deck shoe, 3:2 naturals, double on any two cards, split up to 4 hands.
    /// </summary>
    public sealed class ClassicRules : IRuleSet
    {
        public string DisplayName => "Classic (Vegas)";
        public int DeckCount => 6;
        public float BlackjackPayout => 1.5f;
        public bool DealerHitsSoft17 => true;
        public bool DealerPeeksForBlackjack => true;
        public int MaxSplits => 4;
        public bool DoubleAfterSplitAllowed => true;
        public bool RestrictDoubleTo9To11 => false;
        public bool SurrenderAllowed => false;

        public bool CanDouble(Hand hand)
        {
            // Any two-card hand may double. DAS handled by the engine using MaxSplits.
            if (hand.Cards.Count != 2) return false;
            if (hand.IsSplitHand && !DoubleAfterSplitAllowed) return false;
            return true;
        }

        public bool CanSplit(Hand hand, int currentHandCount)
        {
            return hand.CanSplit && currentHandCount < MaxSplits;
        }
    }
}
