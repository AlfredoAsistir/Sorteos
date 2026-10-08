using eLotto.Core.Repository;
using eLotto.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[Authorize]
[ApiController]
[Route("[controller]")]
public sealed class ReferralProgramBenefitsController : ControllerBase
{
    private readonly IReferralProgramSettingsRepository _repository;

    public ReferralProgramBenefitsController(IReferralProgramSettingsRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        if (settings == null)
            return NotFound(new { message = "No existe la configuración del programa de referidos." });

        return Ok(new ReferralProgramBenefitsResponse(
            settings.IsActive,
            settings.DepositRewardPercentage,
            settings.MaxRewardedDeposits,
            settings.WinnerCashRewardAmount,
            settings.MinimumConfirmedTickets));
    }
}
