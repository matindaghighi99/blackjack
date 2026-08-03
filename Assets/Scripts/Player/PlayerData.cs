using System;

namespace BlackjackGame.Player
{
    /// <summary>
    /// Pure serializable snapshot of a player's persistent state. Used for local saves
    /// (JSON via PlayerPrefs / file) and as the payload synced with the backend.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string PlayerId;
        public string DisplayName = "Guest";

        public long Chips = 0;

        // Progression / stats
        public int Level = 1;
        public int Xp = 0;
        public int HandsPlayed = 0;
        public int HandsWon = 0;
        public int Blackjacks = 0;

        // Daily reward tracking (ISO-8601 UTC string; empty = never claimed)
        public string LastDailyClaimUtc = "";
        public int DailyStreak = 0;

        public PlayerData() { }

        public PlayerData(string playerId, string displayName, long startingChips)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            Chips = startingChips;
        }

        public float WinRate => HandsPlayed == 0 ? 0f : (float)HandsWon / HandsPlayed;
    }
}
