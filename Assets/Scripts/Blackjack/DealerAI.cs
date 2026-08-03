using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Blackjack.Rules;

namespace BlackjackGame.Blackjack
{
    /// <summary>
    /// Deterministic dealer behaviour. The dealer has no discretion in blackjack: it
    /// follows a fixed policy defined by the active <see cref="IRuleSet"/> (stand on 17,
    /// optionally hit soft 17). Kept as a pure decision function for easy testing.
    /// </summary>
    public sealed class DealerAI
    {
        private readonly IRuleSet _rules;

        public DealerAI(IRuleSet rules)
        {
            _rules = rules;
        }

        /// <summary>Returns true if the dealer must draw another card for this hand.</summary>
        public bool ShouldHit(Hand dealerHand)
        {
            var score = HandEvaluator.Evaluate(dealerHand.Cards);

            if (score.Value < 17) return true;
            if (score.Value == 17 && score.IsSoft && _rules.DealerHitsSoft17) return true;

            return false;
        }

        /// <summary>Plays the dealer hand to completion against the shoe.</summary>
        public void PlayOut(Hand dealerHand, Deck deck)
        {
            while (ShouldHit(dealerHand))
            {
                dealerHand.Add(deck.Draw());
            }
        }
    }
}
