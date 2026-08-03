using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using BlackjackGame.Utils; // UnityWebRequest await extension

namespace BlackjackGame.Economy.IAP
{
    /// <summary>
    /// Server-authoritative receipt validation (recommended). Posts the receipt to the
    /// backend's <c>/api/iap/validate</c> endpoint, which verifies it against Apple/Google
    /// and records the transaction to block replays. The client trusts the server's verdict
    /// rather than validating locally (which a tampered client could bypass).
    /// </summary>
    public sealed class BackendReceiptValidator : IReceiptValidator
    {
        private readonly string _endpoint;
        private readonly Func<string> _playerIdProvider;

        public BackendReceiptValidator(string backendBaseUrl, Func<string> playerIdProvider)
        {
            var baseUrl = (backendBaseUrl ?? string.Empty).TrimEnd('/');
            _endpoint = $"{baseUrl}/api/iap/validate";
            _playerIdProvider = playerIdProvider;
        }

        [Serializable]
        private class ValidateRequest
        {
            public string playerId;
            public string platform;
            public string productId;
            public string transactionId;
            public string receipt;
        }

        [Serializable]
        private class ValidateResponse
        {
            public bool valid;
            public string message;
        }

        public async Task<ValidationResult> ValidateAsync(PurchaseReceipt receipt)
        {
            var body = new ValidateRequest
            {
                playerId = _playerIdProvider?.Invoke() ?? "",
                platform = receipt.Platform,
                productId = receipt.ProductId,
                transactionId = receipt.TransactionId,
                receipt = receipt.Payload,
            };

            string json = JsonUtility.ToJson(body);

            using var req = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;

            try
            {
                await req.SendWebRequest();
            }
            catch (Exception e)
            {
                // Fail closed: never grant chips if we couldn't verify.
                return ValidationResult.Invalid($"Validation request error: {e.Message}");
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                return ValidationResult.Invalid($"Validation HTTP {(int)req.responseCode}: {req.error}");
            }

            try
            {
                var resp = JsonUtility.FromJson<ValidateResponse>(req.downloadHandler.text);
                return resp != null && resp.valid
                    ? ValidationResult.Valid(resp.message)
                    : ValidationResult.Invalid(resp?.message ?? "Rejected by server");
            }
            catch (Exception e)
            {
                return ValidationResult.Invalid($"Bad validation response: {e.Message}");
            }
        }
    }
}
