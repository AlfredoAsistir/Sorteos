using System.Security.Claims;
using eLotto.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[Authorize(Roles = "User")]
[ApiController]
[Route("api/my-referrals")]
public sealed class MyReferralsController : ControllerBase
{
    private readonly IMyReferralsService _service;

    public MyReferralsController(IMyReferralsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        return Ok(await _service.GetAsync(userId, cancellationToken));
    }
}
