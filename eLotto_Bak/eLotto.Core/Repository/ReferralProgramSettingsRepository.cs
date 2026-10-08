using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Core.Repository;

public enum ReferralProgramSettingsUpdateStatus
{
    Updated,
    NotFound,
    ConcurrencyConflict
}

public sealed record ReferralProgramSettingsUpdateResult(
    ReferralProgramSettingsUpdateStatus Status,
    ReferralProgramSettings Settings = null);

public interface IReferralProgramSettingsRepository
{
    Task<ReferralProgramSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<ReferralProgramSettingsUpdateResult> UpdateAsync(
        ReferralProgramSettings values,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default);
}

public sealed class ReferralProgramSettingsRepository : IReferralProgramSettingsRepository
{
    private readonly eLottoContext _context;

    public ReferralProgramSettingsRepository(eLottoContext context)
    {
        _context = context;
    }

    public Task<ReferralProgramSettings> GetAsync(CancellationToken cancellationToken = default) =>
        _context.ReferralProgramSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(settings => settings.Id == 1, cancellationToken);

    public async Task<ReferralProgramSettingsUpdateResult> UpdateAsync(
        ReferralProgramSettings values,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var current = await _context.ReferralProgramSettings
            .SingleOrDefaultAsync(settings => settings.Id == 1, cancellationToken);
        if (current == null)
            return new(ReferralProgramSettingsUpdateStatus.NotFound);

        current.IsActive = values.IsActive;
        current.DepositRewardPercentage = values.DepositRewardPercentage;
        current.MaxRewardedDeposits = values.MaxRewardedDeposits;
        current.WinnerCashRewardAmount = values.WinnerCashRewardAmount;
        current.MinimumConfirmedTickets = values.MinimumConfirmedTickets;
        current.UpdatedAt = ApplicationClock.Now;
        _context.Entry(current).Property(settings => settings.RowVersion).OriginalValue = expectedRowVersion;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new(ReferralProgramSettingsUpdateStatus.Updated, current);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.Entry(current).State = EntityState.Detached;
            return new(ReferralProgramSettingsUpdateStatus.ConcurrencyConflict);
        }
    }
}
