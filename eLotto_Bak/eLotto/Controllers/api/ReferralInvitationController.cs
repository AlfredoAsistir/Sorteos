using System.Security.Claims;
using eLotto.Core.Repository;
using eLotto.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[Authorize(Roles = "User")]
[ApiController]
[Route("[controller]")]
public sealed class ReferralInvitationController : ControllerBase
{
    private readonly IUserRepository _users;

    public ReferralInvitationController(IUserRepository users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var referralCode = await _users.GetReferralCodeAsync(userId);
        return string.IsNullOrWhiteSpace(referralCode)
            ? NotFound(new { message = "No fue posible localizar el código de referido del usuario." })
            : Ok(new ReferralInvitationResponse(referralCode));
    }
}
