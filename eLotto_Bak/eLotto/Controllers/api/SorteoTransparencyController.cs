using eLotto.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api;

[ApiController]
[Authorize]
[Route("api/sorteo-transparencia")]
public sealed class SorteoTransparencyController : ControllerBase
{
    private readonly ISorteoTransparencyService _service;
    public SorteoTransparencyController(ISorteoTransparencyService service) => _service = service;

    [AllowAnonymous]
    [HttpGet("{sorteoId:int}")]
    public async Task<IActionResult> GetStatus(int sorteoId, CancellationToken cancellationToken)
    {
        var result = await _service.GetStatusAsync(sorteoId, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{sorteoId:int}/pdf")]
    public async Task<IActionResult> Download(int sorteoId, CancellationToken cancellationToken)
    {
        var result = await _service.GetPdfAsync(sorteoId, cancellationToken);
        return PdfResult(sorteoId, result);
    }

    private IActionResult PdfResult(int sorteoId, (byte[] Content, string Hash)? result)
    {
        if (result == null) return NotFound();
        Response.Headers.ETag = $"\"{result.Value.Hash}\"";
        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, "application/pdf", $"sorteo-{sorteoId}-numeros-no-vendidos.pdf");
    }
}
