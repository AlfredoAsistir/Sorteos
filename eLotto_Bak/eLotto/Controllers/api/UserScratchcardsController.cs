using System.Security.Claims;
using eLotto.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api
{
    [Authorize(Roles = "User")]
    [ApiController]
    [Route("api/user-scratchcards")]
    public sealed class UserScratchcardsController : ControllerBase
    {
        private readonly IScratchcardRevealService _service;

        public UserScratchcardsController(IScratchcardRevealService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            return Ok(await _service.GetListAsync(
                GetUserId(),
                cancellationToken));
        }

        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> Start(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.StartAsync(
                    id,
                    GetUserId(),
                    cancellationToken);
                return result == null
                    ? NotFound(new { message = "No se encontró el rascadito." })
                    : Ok(result);
            }
            catch (ScratchcardDataIntegrityException)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "No fue posible preparar el rascadito.",
                    detail: "Se detectó una inconsistencia y no se modificó la cartera.");
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }
        [HttpPost("{id:int}/reveal")]
        public async Task<IActionResult> Reveal(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.RevealAsync(
                    id,
                    GetUserId(),
                    cancellationToken);
                return result == null
                    ? NotFound(new { message = "No se encontró el rascadito." })
                    : Ok(result);
            }
            catch (ScratchcardDataIntegrityException)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "No fue posible revelar el rascadito.",
                    detail: "Se detectó una inconsistencia y no se realizó ningún abono.");
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        private int GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId)
                ? userId
                : throw new UnauthorizedAccessException(
                    "El token no contiene el usuario.");
        }
    }
}
