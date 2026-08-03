using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackjackGame.Economy.IAP
{
    /// <summary>
    /// Editor / development stand-in for a real billing SDK. Simulates a successful,
    /// instantly-validated purchase so the store flow can be exercised without Unity IAP
    /// or a store account. Selected automatically in the editor (see AppManager).
    /// </summary>
    public sealed class MockPurchaseService : IPurchaseService
    {
        private HashSet<string> _products = new HashSet<string>();

        public bool IsInitialized { get; private set; }

        public event Action OnInitialized;
        public event Action<string> OnInitializeFailed;
        public event Action<string> OnProductPurchased;
        public event Action<string, string> OnPurchaseFailed;

        public void Initialize(IReadOnlyList<string> productIds)
        {
            _products = new HashSet<string>(productIds);
            IsInitialized = true;
            Debug.Log($"[MockPurchaseService] Initialized with {_products.Count} products (no real billing).");
            OnInitialized?.Invoke();
        }

        public void Buy(string productId)
        {
            if (!IsInitialized)
            {
                OnPurchaseFailed?.Invoke(productId, "Store not initialized");
                return;
            }
            if (!_products.Contains(productId))
            {
                OnPurchaseFailed?.Invoke(productId, $"Unknown product '{productId}'");
                return;
            }

            Debug.Log($"[MockPurchaseService] Simulating successful purchase of '{productId}'.");
            OnProductPurchased?.Invoke(productId);
        }
    }
}
