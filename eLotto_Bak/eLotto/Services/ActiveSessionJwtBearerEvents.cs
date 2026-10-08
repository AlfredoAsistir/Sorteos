using eLotto.Core.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace eLotto.Services
{
    public class ActiveSessionJwtBearerEvents : JwtBearerEvents
    {
        private static readonly object SessionReplacedItemKey = new();
        private readonly IUserRepository _userRepository;

        public ActiveSessionJwtBearerEvents(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public override async Task TokenValidated(TokenValidatedContext context)
        {
            var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionIdValue = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (!int.TryParse(userIdValue, out var userId) ||
                !Guid.TryParse(sessionIdValue, out var sessionId))
            {
                context.Fail("La sesión no es válida.");
                return;
            }

            var activeSessionId = await _userRepository.GetActiveSessionIdAsync(userId);
            if (activeSessionId != sessionId)
            {
                context.HttpContext.Items[SessionReplacedItemKey] = true;
                context.Fail("La sesión fue reemplazada por un inicio de sesión más reciente.");
            }
        }

        public override async Task Challenge(JwtBearerChallengeContext context)
        {
            if (!context.HttpContext.Items.ContainsKey(SessionReplacedItemKey))
                return;

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                Code = "session_replaced",
                Message = "Se inició sesión en otro lugar."
            });
        }
    }
}