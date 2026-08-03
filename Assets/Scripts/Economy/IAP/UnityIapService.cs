// Real Unity IAP implementation. Compiled only when the Unity In-App Purchasing package
// (com.unity.purchasing) is installed, which defines UNITY_PURCHASING. Until then the app
// falls back to MockPurchaseService and still compiles cleanly.
#if UNITY_PURCHASING
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extensions;

namespace BlackjackGame.Economy.IAP
{
    /// <summary>
    /// Production billing service backed by Unity IAP (which wraps StoreKit on iOS and
    /// Google Play Billing on Android). Every purchase is held as <b>Pending</b> until its
    /// receipt passes <see cref="IReceiptValidator"/>; only then are chips granted and the
    /// transaction confirmed. This prevents granting on forged or replayed receipts.
    /// </summary>
    public sealed class UnityIapService : IDetailedStoreListener, IPurchaseService
    {
        private readonly IReceiptValidator _validator;
        private IStoreController _controller;
        private IReadOnlyList<string> _productIds = Array.Empty<string>();

        public bool IsInitialized => _controller != null;

        public event Action OnInitialized;
        public event Action<string> OnInitializeFailed;
        public event Action<string> OnProductPurchased;
        public event Action<string, string> OnPurchaseFailed;

        public UnityIapService(IReceiptValidator validator)
        {
            _validator = validator ?? new NoOpReceiptValidator();
        }

        public void Initialize(IReadOnlyList<string> productIds)
        {
            _productIds = productIds ?? Array.Empty<string>();

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var id in _productIds)
            {
                // All chip packs are consumables (can be bought repeatedly).
                builder.AddProduct(id, ProductType.Consumable);
            }

            UnityPurchasing.Initialize(this, builder);
        }

        public void Buy(string productId)
        {
            if (!IsInitialized)
            {
                OnPurchaseFailed?.Invoke(productId, "Store not initialized");
                return;
            }

            var product = _controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                OnPurchaseFailed?.Invoke(productId, "Product unavailable");
                return;
            }

            _controller.InitiatePurchase(product);
        }

        // ---- IStoreListener callbacks ----

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            Debug.Log("[UnityIapService] Store initialized.");
            OnInitialized?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            string reason = $"{error}{(string.IsNullOrEmpty(message) ? "" : $" — {message}")}";
            Debug.LogError($"[UnityIapService] Initialize failed: {reason}");
            OnInitializeFailed?.Invoke(reason);
        }

        /// <summary>
        /// Called by Unity IAP for each purchase. We return <see cref="PurchaseProcessingResult.Pending"/>
        /// and finish asynchronously once the receipt is validated.
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
            ValidateAndComplete(product); // fire-and-forget; confirms the pending purchase itself
            return PurchaseProcessingResult.Pending;
        }

        private async void ValidateAndComplete(Product product)
        {
            var receipt = new PurchaseReceipt(
                productId: product.definition.id,
                platform: CurrentPlatform(),
                payload: product.receipt,
                transactionId: product.transactionID);

            ValidationResult result;
            try
            {
                result = await _validator.ValidateAsync(receipt);
            }
            catch (Exception e)
            {
                result = ValidationResult.Invalid($"Validator threw: {e.Message}");
            }

            if (result.IsValid)
            {
                OnProductPurchased?.Invoke(product.definition.id);
                _controller.ConfirmPendingPurchase(product); // consume it
            }
            else
            {
                Debug.LogWarning($"[UnityIapService] Receipt rejected for {product.definition.id}: {result.Message}");
                OnPurchaseFailed?.Invoke(product.definition.id, result.Message);
                // Consume so a rejected/forged receipt doesn't re-prompt forever.
                _controller.ConfirmPendingPurchase(product);
            }
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            OnPurchaseFailed?.Invoke(product?.definition?.id ?? "?", failureReason.ToString());
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            string id = product?.definition?.id ?? failureDescription?.productId ?? "?";
            OnPurchaseFailed?.Invoke(id, failureDescription?.message ?? failureDescription?.reason.ToString());
        }

        private static string CurrentPlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.OSXPlayer:
                    return "Apple";
                case RuntimePlatform.Android:
                    return "GooglePlay";
                default:
                    return "Fake";
            }
        }
    }
}
#endif
