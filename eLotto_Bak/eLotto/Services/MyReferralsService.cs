using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using eLotto.Models;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Services;

public interface IMyReferralsService
{
    Task<MyReferralsResponse> GetAsync(int referrerUserId, CancellationToken cancellationToken);
}

public sealed class MyReferralsService : IMyReferralsService
{
    private readonly eLottoContext _context;
    private readonly ISorteoTimeService _sorteoTimeService;

    public MyReferralsService(eLottoContext context, ISorteoTimeService sorteoTimeService)
    {
        _context = context;
        _sorteoTimeService = sorteoTimeService;
    }

    public async Task<MyReferralsResponse> GetAsync(
        int referrerUserId,
        CancellationToken cancellationToken)
    {
        var directReferrals = await _context.Users
            .AsNoTracking()
            .Where(user => user.ReferredByUserId == referrerUserId)
            .OrderByDescending(user => user.Date)
            .ThenBy(user => user.Name)
            .Select(user => new { user.Id, user.Name, RegisteredAt = user.Date })
            .ToListAsync(cancellationToken);

        var depositRewards = await _context.ReferralDepositRewards
            .AsNoTracking()
            .Where(reward => reward.ReferrerUserId == referrerUserId)
            .Select(reward => new
            {
                reward.Id,
                reward.ReferredUserId,
                ReferredUserName = reward.ReferredUser.Name,
                reward.CreatedAt,
                ZonaHoraria = reward.SourceDepositTransaction.Sorteo.ZonaHoraria,
                reward.DepositAmount,
                reward.PercentageApplied,
                reward.RewardAmount
            })
            .ToListAsync(cancellationToken);

        var cashRewards = await (
            from reward in _context.ReferralWinnerCashRewards.AsNoTracking()
            join winner in _context.GanadoresSorteos.AsNoTracking()
                on reward.WinnerRecordId equals winner.Id
            join winnerUser in _context.Users.AsNoTracking()
                on winner.UsuarioIdGanador equals winnerUser.Id
            where reward.ReferrerUserId == referrerUserId
            select new
            {
                reward.Id,
                WinnerUserId = winner.UsuarioIdGanador,
                WinnerUserName = winnerUser.Name,
                LotteryName = winner.Nombre,
                reward.CreatedAt,
                ZonaHoraria = winner.Sorteo.ZonaHoraria,
                reward.RequiredTickets,
                reward.ActualTickets,
                reward.RewardAmount,
                reward.Status,
                reward.PaidAt
            })
            .ToListAsync(cancellationToken);

        var depositTotalsByUser = depositRewards
            .GroupBy(reward => reward.ReferredUserId)
            .ToDictionary(
                group => group.Key,
                group => new { Count = group.Count(), Total = group.Sum(reward => reward.RewardAmount) });
        var cashRewardCountsByWinner = cashRewards
            .GroupBy(reward => reward.WinnerUserId)
            .ToDictionary(group => group.Key, group => group.Count());

        var referrals = directReferrals.Select(referral =>
        {
            depositTotalsByUser.TryGetValue(referral.Id, out var depositSummary);
            cashRewardCountsByWinner.TryGetValue(referral.Id, out var cashRewardCount);
            return new DirectReferralResponse(
                referral.Name,
                referral.RegisteredAt,
                depositSummary?.Count ?? 0,
                depositSummary?.Total ?? 0m,
                cashRewardCount);
        }).ToList();

        return new MyReferralsResponse(
            referrals.Count,
            depositRewards.Sum(reward => reward.RewardAmount),
            cashRewards.Where(reward => reward.Status == ReferralWinnerCashRewardStatus.Pending)
                .Sum(reward => reward.RewardAmount),
            cashRewards.Where(reward => reward.Status == ReferralWinnerCashRewardStatus.Paid)
                .Sum(reward => reward.RewardAmount),
            referrals,
            depositRewards.OrderByDescending(reward => _sorteoTimeService.ResolveRecordedTime(
                    reward.CreatedAt, new Sorteos { ZonaHoraria = reward.ZonaHoraria }))
                .ThenByDescending(reward => reward.Id)
                .Select(reward => new ReferralDepositRewardResponse(
                reward.ReferredUserName,
                reward.CreatedAt,
                reward.DepositAmount,
                reward.PercentageApplied,
                reward.RewardAmount)).ToList(),
            cashRewards.OrderByDescending(reward => _sorteoTimeService.ResolveRecordedTime(
                    reward.CreatedAt, new Sorteos { ZonaHoraria = reward.ZonaHoraria }))
                .ThenByDescending(reward => reward.Id)
                .Select(reward => new ReferralWinnerCashRewardResponse(
                reward.WinnerUserName,
                reward.LotteryName,
                reward.CreatedAt,
                reward.RequiredTickets,
                reward.ActualTickets,
                reward.RewardAmount,
                reward.Status,
                reward.PaidAt)).ToList());
    }
}
