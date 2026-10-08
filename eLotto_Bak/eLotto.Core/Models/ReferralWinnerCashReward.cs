namespace eLotto.Core.Models;

public sealed class ReferralWinnerCashReward
{
    public long Id { get; set; }
    public int WinnerRecordId { get; set; }
    public int ReferrerUserId { get; set; }
    public int RequiredTickets { get; set; }
    public int ActualTickets { get; set; }
    public decimal RewardAmount { get; set; }
    public ReferralWinnerCashRewardStatus Status { get; set; } = ReferralWinnerCashRewardStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public GanadoresSorteos WinnerRecord { get; set; }
    public Users ReferrerUser { get; set; }
}
