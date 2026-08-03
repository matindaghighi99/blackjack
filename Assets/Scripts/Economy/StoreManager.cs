using System;
using System.Collections.Generic;
using BlackjackGame.Config;
using BlackjackGame.Economy.IAP;
using UnityEngine;

namespace BlackjackGame.Economy
{
    /// <summary>Result of a purchase attempt, surfaced to the UI.</summary>
    public readonly struct PurchaseResult
    {
        public readonly bool Success;
        public readonly string ProductId;
        public readonly string Message;
        public readonly int ChipsGranted;

        public PurchaseResult(bool success, string productId, string message, int chipsGranted)
        {
            Success = success;
            ProductId = productId;
            Message = message;
            ChipsGranted = chipsGranted;
        }
    }

    /// <summary>
    /// Real IAP store front. Delegates the platform billing flow to an
    /// <see cref="IPurchaseService"/> (Unity IAP on device, a mock in the editor) and grants
    /// chips ONLY after <see cref="IPurchaseService.OnProductPurchased"/> fires — i.e. after
    /// the receipt has been validated. Chip pack definitions come from
    /// <see cref="EconomyConfig"/>, and each pack's <c>Id</c> is its store product id.
    ///
    /// Purchasing is asynchronous: call <see cref="PurchasePack"/>, then listen to
    /// <see cref="OnPurchaseCompleted"/> for the outcome.
    /// </summary>
    public sealed class StoreManager : IDisposable
    {
        private readonly EconomyConfig _config;
        private readonly ChipManager _chips;
        private readonly IPurchaseService _purchases;

        /// <summary>Raised for both success and failure once a purchase attempt resolves.</summary>
        public event Action<PurchaseResult> OnPurchaseCompleted;
        /// <summary>Raised when the store finishes initializing (products ready).</summary>
        public event Action OnStoreReady;

        public bool IsReady => _purchases.IsInitialized;
        public IReadOnlyList<ChipPack> AvailablePacks => _config.ChipPacks;

        public StoreManager(EconomyConfig config, ChipManager chips, IPurchaseService purchases)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _chips = chips ?? throw new ArgumentNullException(nameof(chips));
            _purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));

            _purchases.OnInitialized += HandleStoreReady;
            _purchases.OnInitializeFailed += HandleInitializeFailed;
            _purchases.OnProductPurchased += HandleProductPurchased;
            _purchases.OnPurchaseFailed += HandlePurchaseFailed;

            var ids = new List<string>(_config.ChipPacks.Length);
            foreach (var pack in _config.ChipPacks) ids.Add(pack.Id);
            _purchases.Initialize(ids);
        }

        /// <summary>Begins the purchase flow for a pack. Result arrives via <see cref="OnPurchaseCompleted"/>.</summary>
        public void PurchasePack(string packId)
        {
            if (!_purchases.IsInitialized)
            {
                OnPurchaseCompleted?.Invoke(new PurchaseResult(false, packId, "Store not ready yet.", 0));
                return;
            }
            _purchases.Buy(packId);
        }

        private void HandleStoreReady() => OnStoreReady?.Invoke();

        private void HandleInitializeFailed(string reason)
        {
            Debug.LogWarning($"[StoreManager] Store init failed: {reason}");
        }

        /// <summary>Grants chips for a validated purchase. This is the only chip-granting path.</summary>
        private void HandleProductPurchased(string productId)
        {
            if (!TryFindPack(productId, out var pack))
            {
                OnPurchaseCompleted?.Invoke(new PurchaseResult(false, productId,
                    $"Purchased unknown product '{productId}'.", 0));
                return;
            }

            int total = pack.ChipAmount + pack.BonusChips;
            _chips.Add(total);
            OnPurchaseCompleted?.Invoke(new PurchaseResult(true, productId,
                $"Purchase complete — {total:N0} chips added.", total));
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            OnPurchaseCompleted?.Invoke(new PurchaseResult(false, productId,
                $"Purchase failed: {reason}", 0));
        }

        private bool TryFindPack(string productId, out ChipPack pack)
        {
            foreach (var p in _config.ChipPacks)
            {
                if (string.Equals(p.Id, productId, StringComparison.Ordinal))
                {
                    pack = p;
                    return true;
                }
            }
            pack = default;
            return false;
        }

        public void Dispose()
        {
            _purchases.OnInitialized -= HandleStoreReady;
            _purchases.OnInitializeFailed -= HandleInitializeFailed;
            _purchases.OnProductPurchased -= HandleProductPurchased;
            _purchases.OnPurchaseFailed -= HandlePurchaseFailed;
        }
    }
}
