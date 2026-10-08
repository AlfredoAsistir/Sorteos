using System.Security.Claims;
using eLotto.Models;
using eLotto.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[Authorize(Roles = "User")]
[ApiController]
[Route("api/user-lottery")]
public sealed class UserLotteryController : ControllerBase
{
    private readonly IUserLotteryService _service;
    public UserLotteryController(IUserLotteryService service) => _service = service;

    [AllowAnonymous]
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var current = await _service.GetCurrentAsync(cancellationToken);
        return current == null ? NotFound(new { message = "Actualmente no hay un sorteo disponible." }) : Ok(current);
    }

    [HttpGet("my-tickets")]
    public async Task<IActionResult> GetMyTickets(CancellationToken cancellationToken)
    {
        var result = await _service.GetMyTicketsAsync(GetUserId(), cancellationToken);
        return result == null
            ? NotFound(new { message = "Actualmente no hay un sorteo vigente." })
            : Ok(result);
    }

    [HttpPost("my-tickets/{folio}/whatsapp")]
    public async Task<IActionResult> ResendTickets(
        string folio,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var result = await _service.ResendTicketsAsync(
                GetUserId(),
                folio,
                cancellationToken);
            if (result.Ok)
                return Ok(result);

            Response.Headers["Retry-After"] = result.ReintentarEnSegundos.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, result);
        });

    [HttpPost("manual-query")]
    public async Task<IActionResult> QueryManual([FromBody] ManualTicketQueryDto request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Ok(await _service.QueryManualAsync(GetUserId(), request, cancellationToken)));

    [HttpPost("pre-reserve/manual")]
    public async Task<IActionResult> PreReserveManual([FromBody] ManualPreReserveDto request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var result = await _service.PreReserveManualAsync(GetUserId(), request, cancellationToken);
            return result.Ok ? Ok(result) : Conflict(result);
        });

    [HttpPost("pre-reserve/random")]
    public async Task<IActionResult> PreReserveRandom([FromBody] RandomPreReserveDto request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var result = await _service.PreReserveRandomAsync(GetUserId(), request, cancellationToken);
            return result.Ok ? Ok(result) : Conflict(result);
        });

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase(
        [FromBody] ConfirmTicketPurchaseDto request,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var result = await _service.ConfirmPurchaseAsync(GetUserId(), request, cancellationToken);
            return result.Ok ? Ok(result) : Conflict(result);
        });
    [HttpDelete("pre-reserve")]
    public async Task<IActionResult> Release(CancellationToken cancellationToken)
    {
        await _service.ReleaseAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : throw new UnauthorizedAccessException("El token no contiene el usuario.");
    }

    private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (InvalidOperationException ex) { return new BadRequestObjectResult(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return new UnauthorizedResult(); }
    }
}
