namespace eLotto.Core.Models;

public sealed class ReferralProgramSettings
{
    public byte Id { get; set; }
    public bool IsActive { get; set; }
    public decimal DepositRewardPercentage { get; set; }
    public int MaxRewardedDeposits { get; set; }
    public decimal WinnerCashRewardAmount { get; set; }
    public int MinimumConfirmedTickets { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; }
}
