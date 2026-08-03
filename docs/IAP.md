# In-App Purchases (IAP)

Real virtual-chip purchases via **Unity IAP** (StoreKit on iOS, Google Play Billing on
Android) with **server-side receipt validation**. Virtual chips only — no cash value.

## How it fits together

```
StoreUI ──> StoreManager ──> IPurchaseService ─┬─ MockPurchaseService   (editor / no package)
                                               └─ UnityIapService        (device, #if UNITY_PURCHASING)
                                                        │ ProcessPurchase (held as Pending)
                                                        ▼
                                               IReceiptValidator ─┬─ NoOpReceiptValidator      (mock)
                                                                  ├─ BackendReceiptValidator   (server, default)
                                                                  └─ LocalReceiptValidator     (#if RECEIPT_VALIDATION_LOCAL)
                                                        │ valid?
                                                        ▼
                                   StoreManager.HandleProductPurchased ──> ChipManager.Add(chips)
```

**Chips are granted only after a purchase's receipt is validated** — the placeholder
"auto-grant on tap" is gone. `ChipPack.Id` (in `EconomyConfig`) is the store product id.

## Editor / development

Nothing to configure — the editor uses `MockPurchaseService`, which simulates an instant,
validated purchase so you can click through the store flow. Chips are granted through the
same code path as production.

## Enabling real IAP on device

1. **Install the package** — it's already in `Packages/manifest.json`
   (`com.unity.purchasing`). Open the project so Unity resolves it. This defines
   `UNITY_PURCHASING`, which compiles `UnityIapService`.
2. **Enable Unity Gaming Services / IAP** — `Services ▸ In-App Purchasing ▸ Enable`, link a
   Unity project id.
3. **Create products in the stores** — one **consumable** per pack, using the exact ids
   from `EconomyConfig.ChipPacks` (`pack_small`, `pack_medium`, `pack_large`, `pack_mega`):
   - **App Store Connect** → your app → In-App Purchases → Consumable.
   - **Google Play Console** → Monetize → Products → In-app products.
4. **Point the client at your backend** — set `EconomyConfig.BackendBaseUrl` and keep
   `UseServerReceiptValidation = true`.

## Server-side validation (recommended, default)

`BackendReceiptValidator` POSTs each receipt to `POST /api/iap/validate`. The backend:
- rejects a `transactionId` it has already seen (**anti-replay**), and
- verifies the receipt with Apple/Google.

The verifiers in `backend/controllers/iapController.js` are **stubs** that accept receipts
in dev (`IAP_ALLOW_UNVERIFIED=true` or `NODE_ENV != production`). Before shipping:

- **Apple:** POST the receipt to `https://buy.itunes.apple.com/verifyReceipt` with your
  app **shared secret**; on status `21007` retry against the sandbox URL.
- **Google:** call the Play Developer API
  `purchases.products.get(packageName, productId, purchaseToken)` with a service account.

Relevant env vars are documented in `backend/.env.example`
(`APPLE_SHARED_SECRET`, `GOOGLE_SERVICE_ACCOUNT_JSON`, `ANDROID_PACKAGE_NAME`).

> The current economy is client-authoritative (chips live in `PlayerPrefs`); the server
> validates authenticity. For a fully server-authoritative balance, move the chip catalog
> and balance to the backend and have `/api/iap/validate` credit the account directly.

## Local validation (optional, offline)

For simple/offline builds you can validate on-device instead:
1. `Services ▸ In-App Purchasing ▸ Receipt Validation Obfuscator` → generate
   `AppleTangle` & `GooglePlayTangle`.
2. Add the scripting define `RECEIPT_VALIDATION_LOCAL` (Player Settings) to compile
   `LocalReceiptValidator`, and select it in `AppManager.CreatePurchaseService()`.

Note: a tampered client can bypass local checks — prefer server-side for a real economy.

## Testing

- **Editor:** just play; the mock grants chips.
- **Backend endpoint:**
  ```bash
  curl -X POST http://localhost:3000/api/iap/validate \
    -H "Content-Type: application/json" \
    -d '{"platform":"Fake","productId":"pack_medium","transactionId":"t1","receipt":"{}"}'
  # -> {"valid":true,...}; repeating the same transactionId -> 409 already processed
  ```
- **Device:** use App Store **sandbox** testers / Google Play **license testers**.
