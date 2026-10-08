namespace eLotto.Core.Models;

public sealed class ReferralDepositReward
{
    public long Id { get; set; }
    public int ReferredUserId { get; set; }
    public int ReferrerUserId { get; set; }
    public int SourceDepositTransactionId { get; set; }
    public int RewardWalletTransactionId { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal PercentageApplied { get; set; }
    public decimal RewardAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Users ReferredUser { get; set; }
    public Users ReferrerUser { get; set; }
    public WalletTransaction SourceDepositTransaction { get; set; }
    public WalletTransaction RewardWalletTransaction { get; set; }
}
