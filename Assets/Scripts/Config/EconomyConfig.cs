using UnityEngine;

namespace BlackjackGame.Config
{
    /// <summary>A single purchasable chip pack shown in the store (no real-money integration).</summary>
    [System.Serializable]
    public struct ChipPack
    {
        public string Id;
        public string DisplayName;
        public int ChipAmount;
        [Tooltip("Bonus chips added on top of the base amount.")]
        public int BonusChips;
        [Tooltip("Display-only price string, e.g. \"$4.99\". No real billing wired.")]
        public string PriceLabel;
    }

    /// <summary>
    /// Economy tuning: starting balance, daily rewards and mock store inventory.
    /// Keeps all balance/monetisation numbers out of code.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Blackjack/Economy Config", order = 1)]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Balance")]
        [Min(0)] public int StartingChips = 5000;

        [Header("Daily Reward")]
        [Tooltip("Chip reward for each consecutive login day. Index 0 = day 1.")]
        public int[] DailyRewardLadder = { 500, 750, 1000, 1500, 2000, 3000, 5000 };

        [Tooltip("Hours that must pass before the next daily reward can be claimed.")]
        [Min(1)] public int DailyRewardCooldownHours = 20;

        [Header("IAP / Receipt Validation")]
        [Tooltip("Base URL of the backend used for server-side receipt validation.")]
        public string BackendBaseUrl = "http://localhost:3000";

        [Tooltip("When true (recommended), receipts are validated server-side via the backend. " +
                 "When false, purchases are accepted without server validation (dev only).")]
        public bool UseServerReceiptValidation = true;

        [Header("Store — chip packs (Id doubles as the store product id)")]
        public ChipPack[] ChipPacks =
        {
            new ChipPack { Id = "pack_small",  DisplayName = "Handful",  ChipAmount = 10000,  BonusChips = 0,     PriceLabel = "$1.99" },
            new ChipPack { Id = "pack_medium", DisplayName = "Stack",    ChipAmount = 55000,  BonusChips = 5000,  PriceLabel = "$4.99" },
            new ChipPack { Id = "pack_large",  DisplayName = "Vault",    ChipAmount = 150000, BonusChips = 25000, PriceLabel = "$9.99" },
            new ChipPack { Id = "pack_mega",   DisplayName = "Fortune",  ChipAmount = 500000, BonusChips = 125000,PriceLabel = "$19.99" }
        };
    }
}
