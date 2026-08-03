using System;
using BlackjackGame.Config;
using BlackjackGame.Player;

namespace BlackjackGame.Economy
{
    /// <summary>Outcome of a daily-reward claim attempt.</summary>
    public readonly struct DailyRewardResult
    {
        public readonly bool Success;
        public readonly int ChipsAwarded;
        public readonly int NewStreak;
        public readonly TimeSpan TimeUntilNext;

        public DailyRewardResult(bool success, int chips, int streak, TimeSpan untilNext)
        {
            Success = success;
            ChipsAwarded = chips;
            NewStreak = streak;
            TimeUntilNext = untilNext;
        }
    }

    /// <summary>
    /// Daily login reward logic. Streak advances on consecutive claims; missing the
    /// window resets the streak. All numbers come from <see cref="EconomyConfig"/>.
    /// </summary>
    public sealed class RewardSystem
    {
        private readonly EconomyConfig _config;
        private readonly PlayerProfile _profile;
        private readonly ChipManager _chips;

        public RewardSystem(EconomyConfig config, PlayerProfile profile, ChipManager chips)
        {
            _config = config;
            _profile = profile;
            _chips = chips;
        }

        /// <summary>True if the daily reward is currently claimable.</summary>
        public bool IsRewardAvailable(DateTime nowUtc)
        {
            return TimeUntilNext(nowUtc) <= TimeSpan.Zero;
        }

        /// <summary>Time remaining before the next reward unlocks (zero if available now).</summary>
        public TimeSpan TimeUntilNext(DateTime nowUtc)
        {
            var last = ParseLastClaim();
            if (last == null) return TimeSpan.Zero;

            var next = last.Value.AddHours(_config.DailyRewardCooldownHours);
            var remaining = next - nowUtc;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>Attempts to claim today's reward, advancing or resetting the streak.</summary>
        public DailyRewardResult TryClaim(DateTime nowUtc)
        {
            if (!IsRewardAvailable(nowUtc))
            {
                return new DailyRewardResult(false, 0, _profile.Data.DailyStreak, TimeUntilNext(nowUtc));
            }

            var last = ParseLastClaim();

            // Streak breaks if more than two cooldown windows elapsed since last claim.
            int streak = _profile.Data.DailyStreak;
            if (last == null || (nowUtc - last.Value).TotalHours > _config.DailyRewardCooldownHours * 2)
            {
                streak = 0;
            }

            int ladderIndex = Math.Min(streak, _config.DailyRewardLadder.Length - 1);
            int reward = _config.DailyRewardLadder[ladderIndex];

            _chips.Add(reward);

            _profile.Data.DailyStreak = streak + 1;
            _profile.Data.LastDailyClaimUtc = nowUtc.ToString("o");
            _profile.Save();

            return new DailyRewardResult(true, reward, _profile.Data.DailyStreak,
                TimeSpan.FromHours(_config.DailyRewardCooldownHours));
        }

        private DateTime? ParseLastClaim()
        {
            if (string.IsNullOrEmpty(_profile.Data.LastDailyClaimUtc)) return null;
            if (DateTime.TryParse(_profile.Data.LastDailyClaimUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToUniversalTime();
            return null;
        }
    }
}
