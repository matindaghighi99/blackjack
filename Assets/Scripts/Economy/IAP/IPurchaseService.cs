using System;
using System.Collections.Generic;

namespace BlackjackGame.Economy.IAP
{
    /// <summary>
    /// A store receipt to be validated. Platform-agnostic wrapper around whatever the
    /// underlying billing SDK produced, so validators (local or server) don't depend on
    /// Unity IAP types.
    /// </summary>
    public readonly struct PurchaseReceipt
    {
        public readonly string ProductId;
        /// <summary>"Apple", "GooglePlay", or "Fake" (editor/mock).</summary>
        public readonly string Platform;
        /// <summary>Raw receipt payload (Unity IAP <c>product.receipt</c> JSON).</summary>
        public readonly string Payload;
        /// <summary>Store transaction id — used for anti-replay de-duplication.</summary>
        public readonly string TransactionId;

        public PurchaseReceipt(string productId, string platform, string payload, string transactionId)
        {
            ProductId = productId;
            Platform = platform;
            Payload = payload;
            TransactionId = transactionId;
        }
    }

    /// <summary>
    /// Abstraction over the platform billing SDK. <see cref="StoreManager"/> depends only
    /// on this, so the engine can run against a mock in the editor and real Unity IAP on
    /// device without any changes to game code.
    /// </summary>
    public interface IPurchaseService
    {
        bool IsInitialized { get; }

        /// <summary>Raised once the store is ready and products are fetched.</summary>
        event Action OnInitialized;

        /// <summary>Raised if the store fails to initialize (e.g. billing unavailable).</summary>
        event Action<string> OnInitializeFailed;

        /// <summary>
        /// Raised after a purchase has been received AND its receipt validated.
        /// Argument is the product id. This is the only place chips should be granted.
        /// </summary>
        event Action<string> OnProductPurchased;

        /// <summary>Raised when a purchase fails or its receipt fails validation. (productId, reason)</summary>
        event Action<string, string> OnPurchaseFailed;

        /// <summary>Initializes the store with the set of product ids to fetch.</summary>
        void Initialize(IReadOnlyList<string> productIds);

        /// <summary>Starts the platform purchase flow for a product id.</summary>
        void Buy(string productId);
    }
}
