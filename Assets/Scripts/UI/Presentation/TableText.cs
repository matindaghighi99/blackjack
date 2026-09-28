using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BlackjackGame.Blackjack;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Blackjack.Rules;

namespace BlackjackGame.UI.Presentation
{
    /// <summary>How a hand total should be styled.</summary>
    public enum TotalTone
    {
        Normal,
        /// <summary>An ace still counts 11: shown as "7 / 17".</summary>
        Soft,
        TwentyOne,
        Blackjack,
        Bust,
    }

    /// <summary>A hand total ready for display.</summary>
    public readonly struct TotalDisplay
    {
        public readonly string Text;
        public readonly TotalTone Tone;

        public TotalDisplay(string text, TotalTone tone)
        {
            Text = text;
            Tone = tone;
        }
    }

    /// <summary>How a round result should be styled.</summary>
    public enum ResultTone
    {
        Win,
        Blackjack,
        Push,
        Loss,
    }

    /// <summary>
    /// What the result banner says: what happened, how much it was worth, and why.
    /// </summary>
    public readonly struct RoundSummary
    {
        /// <summary>"BLACKJACK", "YOU WIN", "PUSH", "DEALER WINS", "BUST" …</summary>
        public readonly string Headline;
        /// <summary>"+750", "−500", "Bet returned".</summary>
        public readonly string Amount;
        /// <summary>The reason: "20 beats 18", "Dealer busts with 24", "Both have 19".</summary>
        public readonly string Detail;
        public readonly ResultTone Tone;
        /// <summary>Net chips across all hands, straight from the engine's results.</summary>
        public readonly long Net;

        public RoundSummary(string headline, string amount, string detail, ResultTone tone, long net)
        {
            Headline = headline;
            Amount = amount;
            Detail = detail;
            Tone = tone;
            Net = net;
        }
    }

    /// <summary>
    /// Turns engine state into the words and numbers the table shows.
    ///
    /// Pure C# with no Unity dependency, so it is unit-testable and can be driven headless
    /// against the real engine. It never decides anything: totals come from
    /// <see cref="Hand"/> / <see cref="HandEvaluator"/>, outcomes and chip amounts from the
    /// engine's <see cref="HandResult"/>s, and rule wording from the active
    /// <see cref="IRuleSet"/>. This class only chooses how to say it.
    /// </summary>
    public static class TableText
    {
        /// <summary>Fixed formatting culture: grouping must not change with the device locale
        /// mid-layout, and tests need stable strings.</summary>
        public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>True minus sign (U+2212) — a hyphen reads as a dash next to digits.</summary>
        public const string Minus = "−";

        // ---------------------------------------------------------------------
        //  Numbers
        // ---------------------------------------------------------------------

        public static string Chips(long amount) => amount.ToString("N0", Culture);

        /// <summary>"+1,500", "−500", "±0".</summary>
        public static string Signed(long amount)
        {
            if (amount > 0) return "+" + Chips(amount);
            if (amount < 0) return Minus + Chips(-amount);
            return "±0";
        }

        /// <summary>Short denomination for a chip face: 10, 25, 100, 1K, 2.5K, 10K, 1M.</summary>
        public static string ChipLabel(long value)
        {
            if (value >= 1_000_000) return Compact(value, 1_000_000, "M");
            if (value >= 1_000) return Compact(value, 1_000, "K");
            return value.ToString(Culture);
        }

        private static string Compact(long value, long unit, string suffix)
        {
            if (value % unit == 0) return (value / unit).ToString(Culture) + suffix;
            return (value / (double)unit).ToString("0.#", Culture) + suffix;
        }

        /// <summary>The extra stake a double or split commits, e.g. "+500".</summary>
        public static string ExtraStake(int bet) => bet > 0 ? "+" + Chips(bet) : "";

        /// <summary>
        /// The chips that make up <paramref name="amount"/>, largest first, capped at
        /// <paramref name="maxChips"/> — used to build a physical stack on the felt. The
        /// label beside the stack always shows the exact figure, so a capped stack never
        /// misstates the bet.
        /// </summary>
        public static List<int> Breakdown(long amount, IReadOnlyList<int> denominations, int maxChips)
        {
            var chips = new List<int>();
            if (amount <= 0 || denominations == null || maxChips <= 0) return chips;

            var sorted = new List<int>();
            foreach (int d in denominations)
                if (d > 0 && !sorted.Contains(d)) sorted.Add(d);
            sorted.Sort((a, b) => b.CompareTo(a));
            if (sorted.Count == 0) return chips;

            long remaining = amount;
            foreach (int d in sorted)
            {
                while (remaining >= d && chips.Count < maxChips)
                {
                    chips.Add(d);
                    remaining -= d;
                }
            }

            // A remainder below the smallest chip still deserves a chip on the felt.
            if (chips.Count == 0) chips.Add(sorted[sorted.Count - 1]);
            return chips;
        }

        // ---------------------------------------------------------------------
        //  Totals
        // ---------------------------------------------------------------------

        /// <summary>The total for a whole hand, using the engine's own flags.</summary>
        public static TotalDisplay ForHand(Hand hand)
        {
            if (hand == null || hand.Cards.Count == 0) return new TotalDisplay("", TotalTone.Normal);
            return Describe(hand.Value, hand.IsSoft, hand.IsBust, hand.IsBlackjack);
        }

        /// <summary>
        /// The total of just the cards shown — the dealer's up card while the hole card is
        /// down, or a partially revealed dealer hand during the draw.
        /// </summary>
        public static TotalDisplay ForCards(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0) return new TotalDisplay("", TotalTone.Normal);
            HandScore score = HandEvaluator.Evaluate(cards);
            bool bust = score.Value > HandEvaluator.BlackjackTarget;
            return Describe(score.Value, score.IsSoft, bust, HandEvaluator.IsBlackjack(cards));
        }

        private static TotalDisplay Describe(int value, bool soft, bool bust, bool blackjack)
        {
            if (blackjack) return new TotalDisplay("BLACKJACK", TotalTone.Blackjack);
            if (bust) return new TotalDisplay("BUST", TotalTone.Bust);
            if (value == HandEvaluator.BlackjackTarget) return new TotalDisplay("21", TotalTone.TwentyOne);
            // A soft total is the value with an ace as 11; the alternative is the same hand
            // with that ace as 1. Showing both is what players read off a real table.
            if (soft) return new TotalDisplay($"{value - 10} / {value}", TotalTone.Soft);
            return new TotalDisplay(value.ToString(Culture), TotalTone.Normal);
        }

        // ---------------------------------------------------------------------
        //  Round results
        // ---------------------------------------------------------------------

        /// <summary>Headline, amount and reason for a settled round.</summary>
        public static RoundSummary Summarize(IReadOnlyList<HandResult> results, Hand dealer, IRuleSet rules)
        {
            if (results == null || results.Count == 0)
                return new RoundSummary("ROUND OVER", "", "", ResultTone.Push, 0);

            long net = 0;
            foreach (HandResult r in results) net += r.NetChips;

            return results.Count == 1
                ? SummarizeSingle(results[0], dealer, rules, net)
                : SummarizeSplit(results, net);
        }

        private static RoundSummary SummarizeSingle(HandResult r, Hand dealer, IRuleSet rules, long net)
        {
            int player = r.Hand != null ? r.Hand.Value : 0;
            int dealerValue = dealer != null ? dealer.Value : 0;
            bool dealerBust = dealer != null && dealer.IsBust;
            bool dealerBlackjack = dealer != null && dealer.IsBlackjack;

            switch (r.Outcome)
            {
                case HandOutcome.PlayerBlackjack:
                    return new RoundSummary("BLACKJACK", Signed(r.NetChips),
                        rules != null ? $"Blackjack pays {Ratio(rules.BlackjackPayout).ToLowerInvariant()}" : "",
                        ResultTone.Blackjack, net);

                case HandOutcome.PlayerWin:
                    return new RoundSummary("YOU WIN", Signed(r.NetChips),
                        dealerBust ? $"Dealer busts with {dealerValue}" : $"{player} beats {dealerValue}",
                        ResultTone.Win, net);

                case HandOutcome.Push:
                    return new RoundSummary("PUSH", "Bet returned",
                        dealerBlackjack ? "Both have blackjack" : $"Both have {player}",
                        ResultTone.Push, net);

                case HandOutcome.PlayerBust:
                    return new RoundSummary("BUST", Signed(r.NetChips), $"Over 21 with {player}",
                        ResultTone.Loss, net);

                case HandOutcome.Surrendered:
                    return new RoundSummary("SURRENDERED", Signed(r.NetChips), "Half your bet returned",
                        ResultTone.Push, net);

                default: // DealerWin (and any future loss outcome)
                    return new RoundSummary("DEALER WINS", Signed(r.NetChips),
                        dealerBlackjack ? "Dealer has blackjack" : $"{dealerValue} beats {player}",
                        ResultTone.Loss, net);
            }
        }

        private static RoundSummary SummarizeSplit(IReadOnlyList<HandResult> results, long net)
        {
            int won = 0, lost = 0, pushed = 0;
            bool anyBlackjack = false;
            foreach (HandResult r in results)
            {
                switch (r.Outcome)
                {
                    case HandOutcome.PlayerBlackjack: won++; anyBlackjack = true; break;
                    case HandOutcome.PlayerWin: won++; break;
                    case HandOutcome.Push:
                    case HandOutcome.Surrendered: pushed++; break;
                    default: lost++; break;
                }
            }

            var detail = new StringBuilder();
            Append(detail, won, "won");
            Append(detail, lost, "lost");
            Append(detail, pushed, "pushed");

            if (net > 0)
                return new RoundSummary("YOU WIN", Signed(net), detail.ToString(),
                    anyBlackjack ? ResultTone.Blackjack : ResultTone.Win, net);
            if (net < 0)
                return new RoundSummary("DEALER WINS", Signed(net), detail.ToString(), ResultTone.Loss, net);
            return new RoundSummary("EVEN", Signed(0), detail.ToString(), ResultTone.Push, net);
        }

        private static void Append(StringBuilder sb, int count, string word)
        {
            if (count <= 0) return;
            if (sb.Length > 0) sb.Append("  ·  ");
            sb.Append(count.ToString(Culture)).Append(' ').Append(word);
        }

        /// <summary>Short per-hand tag shown under a split hand once it settles.</summary>
        public static string HandResultTag(HandResult r)
        {
            switch (r.Outcome)
            {
                case HandOutcome.PlayerBlackjack: return "BLACKJACK  " + Signed(r.NetChips);
                case HandOutcome.PlayerWin: return "WIN  " + Signed(r.NetChips);
                case HandOutcome.Push: return "PUSH";
                case HandOutcome.PlayerBust: return "BUST  " + Signed(r.NetChips);
                case HandOutcome.Surrendered: return "SURRENDER  " + Signed(r.NetChips);
                default: return "LOSE  " + Signed(r.NetChips);
            }
        }

        public static ResultTone ToneOf(HandOutcome outcome)
        {
            switch (outcome)
            {
                case HandOutcome.PlayerBlackjack: return ResultTone.Blackjack;
                case HandOutcome.PlayerWin: return ResultTone.Win;
                case HandOutcome.Push:
                case HandOutcome.Surrendered: return ResultTone.Push;
                default: return ResultTone.Loss;
            }
        }

        // ---------------------------------------------------------------------
        //  Rules, session, status
        // ---------------------------------------------------------------------

        /// <summary>A payout multiplier as table odds: 1.5 → "3 TO 2", 1.2 → "6 TO 5".</summary>
        public static string Ratio(float payout)
        {
            if (payout <= 0f || float.IsNaN(payout) || float.IsInfinity(payout)) return "EVEN MONEY";
            for (int d = 1; d <= 20; d++)
            {
                double n = payout * d;
                if (Math.Abs(n - Math.Round(n)) < 1e-4)
                    return $"{(long)Math.Round(n)} TO {d}";
            }
            return payout.ToString("0.##", Culture) + " TO 1";
        }

        /// <summary>The felt's headline line, e.g. "BLACKJACK PAYS 3 TO 2".</summary>
        public static string PayoutLine(IRuleSet rules) =>
            rules == null ? "" : "BLACKJACK PAYS " + Ratio(rules.BlackjackPayout);

        /// <summary>The felt's secondary line, from the dealer's standing rule.</summary>
        public static string DealerRuleLine(IRuleSet rules) =>
            rules == null ? "" : rules.DealerHitsSoft17 ? "Dealer hits soft 17" : "Dealer stands on all 17s";

        /// <summary>HUD session line: "CLASSIC · 6 DECKS · 10 – 10,000".</summary>
        public static string SessionLine(IRuleSet rules, int minBet, int maxBet)
        {
            if (rules == null) return "";
            string name = rules.DisplayName ?? "";
            int paren = name.IndexOf('(');
            if (paren > 0) name = name.Substring(0, paren).Trim();
            return $"{name.ToUpperInvariant()}  ·  {rules.DeckCount} DECKS  ·  {Chips(minBet)} – {Chips(maxBet)}";
        }

        /// <summary>"HAND 2 OF 3".</summary>
        public static string HandOf(int index, int count) =>
            $"HAND {(index + 1).ToString(Culture)} OF {count.ToString(Culture)}";

        public const string StatusPlaceBet = "PLACE YOUR BET";
        public const string StatusReady = "READY TO DEAL";
        public const string StatusDealing = "DEALING";
        public const string StatusYourMove = "YOUR MOVE";
        public const string StatusDealerTurn = "DEALER'S TURN";
        public const string StatusOutOfChips = "OUT OF CHIPS  ·  VISIT THE STORE";
        public const string StatusNotEnough = "NOT ENOUGH CHIPS";

        /// <summary>
        /// Status while the dealer's hand plays out, given what is shown so far. Worded as
        /// "has", not "stands on": when every player hand busted the dealer never draws, and
        /// the UI should not re-derive dealer rules to tell those cases apart.
        /// </summary>
        public static string DealerStatus(IReadOnlyList<Card> shown, bool finished)
        {
            if (!finished || shown == null || shown.Count == 0) return StatusDealerTurn;
            HandScore score = HandEvaluator.Evaluate(shown);
            if (score.Value > HandEvaluator.BlackjackTarget) return "DEALER BUSTS";
            if (HandEvaluator.IsBlackjack(shown)) return "DEALER BLACKJACK";
            return $"DEALER HAS {score.Value.ToString(Culture)}";
        }

        /// <summary>The hint under an empty betting spot: "MIN 10  ·  MAX 10,000".</summary>
        public static string Limits(int minBet, int maxBet) =>
            $"MIN {Chips(minBet)}  ·  MAX {Chips(maxBet)}";
    }
}
