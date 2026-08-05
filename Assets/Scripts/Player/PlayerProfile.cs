using System;
using UnityEngine;

namespace BlackjackGame.Player
{
    /// <summary>
    /// Runtime wrapper around <see cref="PlayerData"/> that owns local persistence.
    /// Persistence is deliberately abstracted here so it can later be redirected to the
    /// backend without touching gameplay code.
    /// </summary>
    public sealed class PlayerProfile
    {
        private const string SaveKey = "player_data_v1";

        public PlayerData Data { get; private set; }

        public event Action<PlayerData> OnChanged;

        public PlayerProfile(PlayerData data)
        {
            Data = data;
        }

        /// <summary>Loads the saved profile, or creates a fresh guest profile.</summary>
        public static PlayerProfile LoadOrCreate(long startingChips)
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try
                {
                    var json = PlayerPrefs.GetString(SaveKey);
                    var data = JsonUtility.FromJson<PlayerData>(json);
                    if (data != null && !string.IsNullOrEmpty(data.PlayerId))
                        return new PlayerProfile(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PlayerProfile] Failed to load save, creating new. {e.Message}");
                }
            }

            var fresh = new PlayerData(
                playerId: Guid.NewGuid().ToString("N"),
                displayName: "Guest",
                startingChips: startingChips);
            var profile = new PlayerProfile(fresh);
            profile.Save();
            return profile;
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            OnChanged?.Invoke(Data);
        }

        public void RecordRound(bool won, bool blackjack)
        {
            Data.HandsPlayed++;
            if (won) Data.HandsWon++;
            if (blackjack) Data.Blackjacks++;
            Save();
        }

        /// <summary>
        /// Wipes progress back to a fresh guest state — same player id, everything else
        /// reset. Used by the Settings panel's "Reset Progress" action.
        /// </summary>
        public void ResetProgress(long startingChips)
        {
            string playerId = Data.PlayerId;
            Data = new PlayerData(playerId, "Guest", startingChips);
            Save();
        }
    }
}
