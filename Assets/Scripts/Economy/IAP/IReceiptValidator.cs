using System.Threading.Tasks;

namespace BlackjackGame.Economy.IAP
{
    public readonly struct ValidationResult
    {
        public readonly bool IsValid;
        public readonly string Message;

        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public static ValidationResult Valid(string msg = "ok") => new ValidationResult(true, msg);
        public static ValidationResult Invalid(string msg) => new ValidationResult(false, msg);
    }

    /// <summary>
    /// Strategy for verifying a purchase receipt is genuine before granting chips.
    /// Implementations: <see cref="NoOpReceiptValidator"/> (mock), BackendReceiptValidator
    /// (server-side, recommended), and an optional local validator behind a define.
    /// </summary>
    public interface IReceiptValidator
    {
        Task<ValidationResult> ValidateAsync(PurchaseReceipt receipt);
    }

    /// <summary>Accepts everything. Editor/mock only — NEVER use on a real build.</summary>
    public sealed class NoOpReceiptValidator : IReceiptValidator
    {
        public Task<ValidationResult> ValidateAsync(PurchaseReceipt receipt)
            => Task.FromResult(ValidationResult.Valid("mock - not validated"));
    }
}
