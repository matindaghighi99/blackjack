using System;
using BlackjackGame.UI.Layout;
using NUnit.Framework;

namespace BlackjackGame.Tests
{
    /// <summary>
    /// The responsive table layout, swept across every aspect ratio a device can present.
    /// The canvas scaler (reference 1080x1920, match 0.5) keeps the canvas area constant,
    /// so only the shape varies: 21:9 ultrawide through tall 20:9 phones with notch insets.
    /// </summary>
    public class TableLayoutTests
    {
        private const float Area = 1080f * 1920f;

        private static void ForEveryScreen(Action<TableLayout, string> check)
        {
            for (float aspect = 0.40f; aspect <= 2.40f; aspect += 0.02f)
            {
                float w = (float)Math.Sqrt(Area / aspect);
                float h = w * aspect;
                check(new TableLayout(w, h), $"{w:0}x{h:0}");
                // Notch + home-indicator insets on tall phones, notch sides on wide ones.
                if (aspect >= 1.6f) check(new TableLayout(w, h * 0.9f), $"{w:0}x{h * 0.9f:0} (inset)");
                if (aspect <= 0.6f) check(new TableLayout(w * 0.9f, h), $"{w * 0.9f:0}x{h:0} (inset)");
            }
        }

        [Test]
        public void HandsNeverOverlap_AndTheBannerFitsBetweenThem()
        {
            ForEveryScreen((l, id) =>
            {
                Assert.GreaterOrEqual(l.GapHeight, 0f, id);
                Assert.LessOrEqual(l.BannerHeight, l.GapHeight + 2f, id);
                Assert.LessOrEqual(l.PlayerY + l.PlayerCardHeight / 2f, l.TableBottom + 1f, id);
            });
        }

        [Test]
        public void ControlsAreThumbSized_AndInsideTheScreen()
        {
            ForEveryScreen((l, id) =>
            {
                Slot[] bet = l.BetRow(5);
                Assert.GreaterOrEqual(bet[0].Width, 64f, id + " chip");
                Assert.GreaterOrEqual(bet[6].Width, 160f, id + " deal");
                Slot[] actions = l.ActionRow(false);
                Assert.GreaterOrEqual(actions[0].Width, 90f, id + " double");
                Assert.GreaterOrEqual(actions[1].Width, 130f, id + " hit");
                Assert.GreaterOrEqual(l.ControlLeft, 20f, id);
                Assert.LessOrEqual(l.ControlLeft + l.ControlWidth, l.Width - 20f, id);
            });
        }

        [Test]
        public void SplitSeats_StayOnScreenAndApart()
        {
            ForEveryScreen((l, id) =>
            {
                for (int n = 1; n <= 4; n++)
                {
                    SeatLayout s = l.Seats(n);
                    float half = s.MaxSpan / 2f + TableLayout.CardWidth(s.CardHeight) / 2f;
                    Assert.GreaterOrEqual(s.Xs[0] - half, 0f, $"{id} {n} hands, left edge");
                    Assert.LessOrEqual(s.Xs[n - 1] + half, l.Width, $"{id} {n} hands, right edge");
                    for (int i = 1; i < n; i++)
                        Assert.GreaterOrEqual(s.Xs[i] - s.Xs[i - 1], 2f * half - 1f, $"{id} {n} hands overlap");

                    // A three-card hand must fan wide enough to show every index (~22% of a card).
                    float cw = TableLayout.CardWidth(s.CardHeight);
                    Assert.GreaterOrEqual(Math.Min(cw * 0.62f, s.MaxSpan / 2f), cw * 0.22f, $"{id} {n} hands: indices hidden");
                }
            });
        }

        [Test]
        public void Orientation_FollowsTheScreenShape()
        {
            Assert.IsTrue(new TableLayout(980f, 1916f).Portrait, "phone");
            Assert.IsTrue(new TableLayout(1247f, 1662f).Portrait, "tablet upright");
            Assert.IsFalse(new TableLayout(1920f, 1080f).Portrait, "desktop");
            Assert.IsFalse(new TableLayout(1662f, 1247f).Portrait, "tablet on its side");
        }
    }
}
