using System;
using System.Collections.Generic;
using BlackjackGame.Config;

namespace BlackjackGame.Economy
{
    /// <summary>Result of a (mock) purchase attempt.</summary>
    public readonly struct PurchaseResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly int ChipsGranted;

        public PurchaseResult(bool success, string message, int chipsGranted)
        {
            Success = success;
            Message = message;
            ChipsGranted = chipsGranted;
        }
    }

    /// <summary>
    /// Mock in-app-purchase store. Chip packs come from <see cref="EconomyConfig"/>.
    ///
    /// PLACEHOLDER ONLY: there is no real billing here. To ship, replace
    /// <see cref="PurchasePack"/>'s auto-grant with Unity IAP / StoreKit / Google Play
    /// Billing and grant chips only from a verified purchase receipt.
    /// </summary>
    public sealed class StoreManager
    {
        private readonly EconomyConfig _config;
        private readonly ChipManager _chips;

        public StoreManager(EconomyConfig config, ChipManager chips)
        {
            _config = config;
            _chips = chips;
        }

        public IReadOnlyList<ChipPack> AvailablePacks => _config.ChipPacks;

        /// <summary>
        /// Simulates buying a pack by id and immediately grants the chips.
        /// TODO: gate behind a real, receipt-verified purchase flow before release.
        /// </summary>
        public PurchaseResult PurchasePack(string packId)
        {
            foreach (var pack in _config.ChipPacks)
            {
                if (!string.Equals(pack.Id, packId, StringComparison.Ordinal)) continue;

                int total = pack.ChipAmount + pack.BonusChips;
                _chips.Add(total);
                return new PurchaseResult(true, $"Granted {total:N0} chips (mock purchase).", total);
            }

            return new PurchaseResult(false, $"Unknown pack '{packId}'.", 0);
        }
    }
}
