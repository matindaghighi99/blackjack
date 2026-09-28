using System.Collections.Generic;
using BlackjackGame.Blackjack;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Blackjack.Rules;
using BlackjackGame.UI.Presentation;
using BlackjackGame.Utils;
using NUnit.Framework;

namespace BlackjackGame.Tests
{
    /// <summary>
    /// The table's words and numbers. Everything TableText says is derived from the engine,
    /// so these tests drive the real engine and check the presentation of its results —
    /// they never re-derive a rule.
    /// </summary>
    public class TableTextTests
    {
        private static Hand HandOf(params (Suit, Rank)[] cards)
        {
            var hand = new Hand { Bet = 100 };
            foreach (var (s, r) in cards) hand.Add(new Card(s, r));
            return hand;
        }

        [Test]
        public void Totals_UseTheEnginesFlags()
        {
            Assert.AreEqual("BLACKJACK", TableText.ForHand(HandOf((Suit.Spades, Rank.Ace), (Suit.Hearts, Rank.King))).Text);
            Assert.AreEqual(TotalTone.Blackjack, TableText.ForHand(HandOf((Suit.Spades, Rank.Ace), (Suit.Hearts, Rank.King))).Tone);

            TotalDisplay soft = TableText.ForHand(HandOf((Suit.Spades, Rank.Ace), (Suit.Hearts, Rank.Six)));
            Assert.AreEqual("7 / 17", soft.Text);
            Assert.AreEqual(TotalTone.Soft, soft.Tone);

            TotalDisplay bust = TableText.ForHand(HandOf((Suit.Spades, Rank.King), (Suit.Hearts, Rank.Queen), (Suit.Clubs, Rank.Two)));
            Assert.AreEqual("BUST", bust.Text);

            TotalDisplay three21 = TableText.ForHand(HandOf((Suit.Spades, Rank.Seven), (Suit.Hearts, Rank.Seven), (Suit.Clubs, Rank.Seven)));
            Assert.AreEqual("21", three21.Text);
            Assert.AreEqual(TotalTone.TwentyOne, three21.Tone);

            Assert.AreEqual("", TableText.ForHand(new Hand()).Text, "An empty hand shows no total rather than a zero.");
        }

        [Test]
        public void DealerUpCard_ShowsOnlyWhatIsVisible()
        {
            var up = new List<Card> { new Card(Suit.Hearts, Rank.Ace) };
            Assert.AreEqual("1 / 11", TableText.ForCards(up).Text);
        }

        [Test]
        public void Numbers_AreFormattedForTheTable()
        {
            Assert.AreEqual("12,450", TableText.Chips(12450));
            Assert.AreEqual("+750", TableText.Signed(750));
            Assert.AreEqual("−500", TableText.Signed(-500));
            Assert.AreEqual("1K", TableText.ChipLabel(1000));
            Assert.AreEqual("2.5K", TableText.ChipLabel(2500));
            Assert.AreEqual("3 TO 2", TableText.Ratio(1.5f));
            Assert.AreEqual("6 TO 5", TableText.Ratio(1.2f));
        }

        [Test]
        public void Breakdown_ComposesTheFewestChips()
        {
            CollectionAssert.AreEqual(new[] { 1000, 500, 100, 25, 10 },
                TableText.Breakdown(1635, new[] { 10, 25, 100, 500, 1000 }, 10));
            Assert.AreEqual(8, TableText.Breakdown(100000, new[] { 1000 }, 8).Count, "Stacks are capped.");
            Assert.IsEmpty(TableText.Breakdown(0, new[] { 10 }, 8));
        }

        [Test]
        public void RuleWording_ComesFromTheRuleSet()
        {
            Assert.AreEqual("BLACKJACK PAYS 3 TO 2", TableText.PayoutLine(new ClassicRules()));
            Assert.AreEqual("Dealer hits soft 17", TableText.DealerRuleLine(new ClassicRules()));
            Assert.AreEqual("Dealer stands on all 17s", TableText.DealerRuleLine(new EuropeanRules()));
        }

        [Test]
        public void EverySettledRound_HasAHeadlineAmountAndReason()
        {
            var rules = new ClassicRules();
            var outcomes = new HashSet<HandOutcome>();

            for (int seed = 0; seed < 4000; seed++)
            {
                var engine = new BlackjackEngine(rules, new SystemRandomProvider(seed));
                IReadOnlyList<HandResult> results = null;
                engine.OnRoundSettled += r => results = r;
                engine.StartRound(100);

                // A simple, deterministic line of play that reaches every outcome.
                int guard = 0;
                while (engine.Phase == RoundPhase.PlayerTurn && guard++ < 20)
                {
                    if (engine.CanSplit && seed % 3 == 0) engine.Split();
                    else if (engine.CanDouble && seed % 5 == 0) engine.DoubleDown();
                    else if (engine.ActiveHand.Value < 15) engine.Hit();
                    else engine.Stand();
                }

                Assert.IsNotNull(results, $"seed {seed}: no results");
                RoundSummary summary = TableText.Summarize(results, engine.DealerHand, rules);
                Assert.IsNotEmpty(summary.Headline, $"seed {seed}");
                Assert.IsNotEmpty(summary.Amount, $"seed {seed}");
                Assert.IsNotEmpty(summary.Detail, $"seed {seed}");

                long net = 0;
                foreach (HandResult r in results)
                {
                    net += r.NetChips;
                    outcomes.Add(r.Outcome);
                }
                Assert.AreEqual(net, summary.Net, $"seed {seed}: the banner must report the engine's own net");
            }

            foreach (HandOutcome o in new[] { HandOutcome.PlayerBlackjack, HandOutcome.PlayerWin, HandOutcome.DealerWin,
                         HandOutcome.Push, HandOutcome.PlayerBust })
                Assert.Contains(o, new List<HandOutcome>(outcomes), $"{o} never came up");
        }
    }
}
