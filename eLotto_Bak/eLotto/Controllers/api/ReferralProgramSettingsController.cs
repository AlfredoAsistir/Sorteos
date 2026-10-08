using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("[controller]")]
public sealed class ReferralProgramSettingsController : ControllerBase
{
    private readonly IReferralProgramSettingsRepository _repository;

    public ReferralProgramSettingsController(IReferralProgramSettingsRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return settings == null
            ? NotFound(new { message = "No existe la configuración del programa de referidos." })
            : Ok(Map(settings));
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateReferralProgramSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _repository.UpdateAsync(
            new ReferralProgramSettings
            {
                Id = 1,
                IsActive = request.IsActive,
                DepositRewardPercentage = request.DepositRewardPercentage,
                MaxRewardedDeposits = request.MaxRewardedDeposits,
                WinnerCashRewardAmount = request.WinnerCashRewardAmount,
                MinimumConfirmedTickets = request.MinimumConfirmedTickets
            },
            request.RowVersion,
            cancellationToken);

        return result.Status switch
        {
            ReferralProgramSettingsUpdateStatus.Updated => Ok(Map(result.Settings)),
            ReferralProgramSettingsUpdateStatus.NotFound =>
                NotFound(new { message = "No existe la configuración del programa de referidos." }),
            ReferralProgramSettingsUpdateStatus.ConcurrencyConflict =>
                Conflict(new
                {
                    message = "La configuración fue modificada por otro administrador. Recarga los datos e intenta nuevamente."
                }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static ReferralProgramSettingsResponse Map(ReferralProgramSettings settings) =>
        new(
            settings.Id,
            settings.IsActive,
            settings.DepositRewardPercentage,
            settings.MaxRewardedDeposits,
            settings.WinnerCashRewardAmount,
            settings.MinimumConfirmedTickets,
            settings.UpdatedAt,
            settings.RowVersion);
}
