using eLotto.Core.Models;

namespace eLotto.Models;

public sealed record MyReferralsResponse(
    int DirectReferralsCount,
    decimal DepositRewardsTotal,
    decimal PendingCashRewardsTotal,
    decimal PaidCashRewardsTotal,
    IReadOnlyList<DirectReferralResponse> Referrals,
    IReadOnlyList<ReferralDepositRewardResponse> DepositRewards,
    IReadOnlyList<ReferralWinnerCashRewardResponse> CashRewards);

public sealed record DirectReferralResponse(
    string Name,
    DateTime RegisteredAt,
    int DepositRewardsCount,
    decimal DepositRewardsTotal,
    int WinnerCashRewardsCount);

public sealed record ReferralDepositRewardResponse(
    string ReferredUserName,
    DateTime CreatedAt,
    decimal DepositAmount,
    decimal PercentageApplied,
    decimal RewardAmount);

public sealed record ReferralWinnerCashRewardResponse(
    string WinnerUserName,
    string LotteryName,
    DateTime CreatedAt,
    int RequiredTickets,
    int ActualTickets,
    decimal RewardAmount,
    ReferralWinnerCashRewardStatus Status,
    DateTime? PaidAt);
