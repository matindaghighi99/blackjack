using System.Collections.Generic;
using BlackjackGame.Blackjack;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Utils;
using NUnit.Framework;

namespace BlackjackGame.Tests
{
    /// <summary>
    /// Edit-mode tests for the pure gameplay core. Because the engine has no Unity
    /// dependencies these run fast and headless in the Test Runner.
    /// </summary>
    public class HandEvaluatorTests
    {
        private static List<Card> Cards(params (Suit, Rank)[] defs)
        {
            var list = new List<Card>();
            foreach (var (s, r) in defs) list.Add(new Card(s, r));
            return list;
        }

        [Test]
        public void AceAndKing_IsBlackjack_ValuedAt21()
        {
            var hand = Cards((Suit.Spades, Rank.Ace), (Suit.Hearts, Rank.King));
            Assert.AreEqual(21, HandEvaluator.Evaluate(hand).Value);
            Assert.IsTrue(HandEvaluator.IsBlackjack(hand));
            Assert.IsTrue(HandEvaluator.Evaluate(hand).IsSoft);
        }

        [Test]
        public void MultipleAces_DemoteToAvoidBust()
        {
            // A + A + 9 = 21 (one ace as 11, one as 1)
            var hand = Cards((Suit.Spades, Rank.Ace), (Suit.Clubs, Rank.Ace), (Suit.Hearts, Rank.Nine));
            Assert.AreEqual(21, HandEvaluator.Evaluate(hand).Value);
        }

        [Test]
        public void SoftBecomesHard_WhenAceMustDemote()
        {
            // A + 6 + K = 17 hard (ace demoted from 11 to 1)
            var hand = Cards((Suit.Spades, Rank.Ace), (Suit.Clubs, Rank.Six), (Suit.Hearts, Rank.King));
            var score = HandEvaluator.Evaluate(hand);
            Assert.AreEqual(17, score.Value);
            Assert.IsFalse(score.IsSoft);
        }

        [Test]
        public void ThreeCardTwentyOne_IsNotBlackjack()
        {
            var hand = Cards((Suit.Spades, Rank.Seven), (Suit.Clubs, Rank.Seven), (Suit.Hearts, Rank.Seven));
            Assert.AreEqual(21, HandEvaluator.Evaluate(hand).Value);
            Assert.IsFalse(HandEvaluator.IsBlackjack(hand));
        }

        [Test]
        public void Deck_DrawsAll52UniqueCards_ForSingleDeck()
        {
            var deck = new Deck(1, new SystemRandomProvider(seed: 42));
            var seen = new HashSet<string>();
            for (int i = 0; i < 52; i++)
                seen.Add(deck.Draw().ShortCode);

            Assert.AreEqual(52, seen.Count, "A single deck should contain 52 unique cards.");
        }
    }
}
