// Optional on-device receipt validation using Unity's CrossPlatformValidator.
//
// Enable by:
//   1) Installing Unity IAP (defines UNITY_PURCHASING), then
//   2) Services ▸ In-App Purchasing ▸ Receipt Validation Obfuscator to generate
//      AppleTangle & GooglePlayTangle, then
//   3) Adding the scripting define RECEIPT_VALIDATION_LOCAL in Player Settings.
//
// Server-side validation (BackendReceiptValidator) is preferred for a real economy because
// a tampered client can bypass local checks. This is provided for offline/simple builds.
#if UNITY_PURCHASING && RECEIPT_VALIDATION_LOCAL
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing.Security;

namespace BlackjackGame.Economy.IAP
{
    public sealed class LocalReceiptValidator : IReceiptValidator
    {
        private readonly CrossPlatformValidator _validator;

        public LocalReceiptValidator()
        {
            // GooglePlayTangle / AppleTangle are generated into the project by the Obfuscator.
            _validator = new CrossPlatformValidator(
                GooglePlayTangle.Data(),
                AppleTangle.Data(),
                Application.identifier);
        }

        public Task<ValidationResult> ValidateAsync(PurchaseReceipt receipt)
        {
            try
            {
                _validator.Validate(receipt.Payload);
                return Task.FromResult(ValidationResult.Valid("local receipt ok"));
            }
            catch (IAPSecurityException e)
            {
                return Task.FromResult(ValidationResult.Invalid($"Invalid receipt: {e.Message}"));
            }
        }
    }
}
#endif
