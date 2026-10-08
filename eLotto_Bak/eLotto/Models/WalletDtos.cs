using System.ComponentModel.DataAnnotations;
using eLotto.Core.Models;

namespace eLotto.Models
{
    public class CreateDepositPaymentIntentRequest
    {
        [Range(1, int.MaxValue)]
        public int SorteoId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [RegularExpression("^(card|oxxo|bank_transfer)$")]
        public string PaymentMethod { get; set; } = "card";

        [StringLength(150)]
        public string Name { get; set; }

        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; }
    }

    public record PaymentIntentResponse(string ClientSecret, int TransactionId, string InstructionUrl = null, bool WhatsAppSent = false);
    public record DepositStatusResponse(
        int TransactionId,
        WalletTransactionStatus Status,
        string StripeMessage,
        string UserMessage);
    public record WalletResponse(
        decimal Balance,
        string Currency,
        string Name,
        string Email,
        decimal MinimumDepositAmount,
        decimal MaximumDepositAmount,
        decimal SuggestedDepositAmount,
        string StripePublishableKey);
    public record WalletTransactionResponse(
        int Id,
        WalletTransactionType Type,
        decimal Amount,
        WalletTransactionStatus Status,
        string Description,
        string StripeMessage,
        string UserMessage,
        DateTime CreatedAt,
        DateTime? CompletedAt);
    public record PagedWalletTransactionsResponse(
        IReadOnlyCollection<WalletTransactionResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
