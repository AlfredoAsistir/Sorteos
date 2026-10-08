using System.ComponentModel.DataAnnotations;

namespace eLotto.Core.Models
{
    public class WalletTransaction
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public int WalletId { get; set; }
        public int? SorteoId { get; set; }
        public WalletTransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public WalletTransactionStatus Status { get; set; }
        public string StripePaymentIntentId { get; set; }
        public string StripeEventId { get; set; }
        public string Description { get; set; }
        public string StripeMessage { get; set; }
        public string UserMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Users User { get; set; }
        public UserWallet Wallet { get; set; }
        public Sorteos Sorteo { get; set; }
    }
}
