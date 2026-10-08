using System.Globalization;
using eLotto.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[ApiController]
[AllowAnonymous]
[Route("api/server-clock")]
public sealed class ServerClockController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var now = ApplicationClock.NowOffset;
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            LocalDateTime = now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)
        });
    }
}
