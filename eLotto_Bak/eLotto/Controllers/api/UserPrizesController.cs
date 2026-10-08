using System.Security.Claims;
using eLotto.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api
{
    [Authorize(Roles = "User")]
    [ApiController]
    [Route("api/user-prizes")]
    public sealed class UserPrizesController : ControllerBase
    {
        private readonly IUserPrizeHistoryService _service;

        public UserPrizesController(IUserPrizeHistoryService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.GetAsync(GetUserId(), cancellationToken));
            }
            catch (ScratchcardDataIntegrityException)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "No fue posible consultar todos tus premios.",
                    detail: "Se detectó un resultado histórico incompleto.");
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