using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Core.Services;
using eLotto.Services;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace eLotto.Controllers.api
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenService _tokenService;
        private readonly IAccountServices _accountServices;
        private readonly IUserRepository _userRepository;

        public AuthController(
            JwtTokenService tokenService,
            IAccountServices accountServices,
            IUserRepository userRepository)
        {
            _tokenService = tokenService;
            _accountServices = accountServices;
            _userRepository = userRepository;
        }

        [Authorize]
        [HttpGet("session")]
        public IActionResult CheckSession() => NoContent();

        [Authorize]
        [HttpGet("theme")]
        public async Task<IActionResult> GetTheme()
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized();

            var isDark = await _userRepository.GetThemeAsync(userId);
            if (isDark == null)
                return NotFound();

            return Ok(new { IsDark = isDark.Value });
        }

        [Authorize]
        [HttpPut("theme")]
        public async Task<IActionResult> UpdateTheme([FromBody] ThemePreferenceRequest request)
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized();

            if (request.IsDark is not bool isDark)
                return BadRequest();

            return await _userRepository.SetThemeAsync(userId, isDark) ? NoContent() : NotFound();
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionIdValue = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!int.TryParse(userIdValue, out var userId) ||
                !Guid.TryParse(sessionIdValue, out var sessionId))
                return Unauthorized();

            await _userRepository.ClearActiveSessionIdAsync(userId, sessionId);
            return NoContent();
        }

        [HttpPost("CreateUsers")]
        public async Task<object> CreateUsers([FromBody] UserCreate model)
        {
            return await _accountServices.CreateUserAsync(model);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await GetLoginUserAsync(request.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                return Unauthorized(new { Ok = false, Message = "Credenciales incorrectas" });

            if (!user.IsActive)
            {
                var sent = await _accountServices.SendLoginConfirmationPinAsync(user);
                return Ok(new
                {
                    Ok = false,
                    RequiresConfirmation = true,
                    ConfirmationSent = sent,
                    MaskedWhatsApp = MaskWhatsApp(user.WhatsApp),
                    Message = sent
                        ? "Enviamos un PIN de 4 dígitos a tu WhatsApp."
                        : "No fue posible validar tu número de WhatsApp. Por favor, contáctanos para poder continuar."
                });
            }

            return await CompleteLoginAsync(user, request);
        }

        [HttpPost("login/resend-confirmation")]
        public async Task<IActionResult> ResendLoginConfirmation([FromBody] LoginRequest request)
        {
            var user = await GetLoginUserAsync(request.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                return Unauthorized(new { Ok = false, Message = "Credenciales incorrectas" });
            if (user.IsActive)
                return BadRequest(new { Ok = false, Message = "La cuenta ya se encuentra activa." });

            var sent = await _accountServices.SendLoginConfirmationPinAsync(user);
            return Ok(new
            {
                Ok = sent,
                ConfirmationSent = sent,
                MaskedWhatsApp = MaskWhatsApp(user.WhatsApp),
                Message = sent
                    ? "Enviamos un PIN nuevo a tu WhatsApp."
                    : "No fue posible validar tu número de WhatsApp. Por favor, contáctanos para poder continuar."
            });
        }

        [HttpPost("login/confirm")]
        public async Task<IActionResult> ConfirmLogin([FromBody] LoginConfirmationRequest request)
        {
            var user = await GetLoginUserAsync(request.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                return Unauthorized(new { Ok = false, Message = "Credenciales incorrectas" });
            if (user.IsActive)
                return await CompleteLoginAsync(user, request);
            if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Length != 4)
                return BadRequest(new { Ok = false, Message = "El PIN debe contener 4 números." });

            var confirmed = await _accountServices.ConfirmLoginAsync(user, request.Pin);
            if (!confirmed)
                return BadRequest(new { Ok = false, Message = "PIN incorrecto." });

            return await CompleteLoginAsync(user, request);
        }

        private async Task<IActionResult> CompleteLoginAsync(Users user, LoginRequest request)
        {
            bool isIos = request.Device?.ToLowerInvariant() == "ios";
            if (isIos && !user.IsIos)
                await _userRepository.SetDeviceAsync(user.Id, true);
            if (!isIos && !string.IsNullOrWhiteSpace(request.Device) && !user.IsAndroid)
                await _userRepository.SetDeviceAsync(user.Id, false);

            // El usuario solo puede tener un rol.
            var userRol = await _userRepository.GetUserRoleAsync(user.Id);
            if (userRol == null)
                return Unauthorized(new { Ok = false, Message = "El usuario no tiene un rol asignado." });
            var roles = new List<string> { userRol.RolName };
            var sessionId = Guid.NewGuid();
            var token = _tokenService.GenerateToken(user.Id, user.User, roles, sessionId);
            if (!await _userRepository.SetActiveSessionIdAsync(user.Id, sessionId))
                return Unauthorized(new { Ok = false, Message = "No fue posible iniciar sesión." });

            return Ok(new
            {
                Ok = true,
                Token = token,
                UserId = user.Id,
                user.User,
                userRol.RolId,
                userRol.RolName,
                userRol.Expire,
                Languaje = user.Languaje,
                IsDark = user.IsDark
            });
        }

        private static string MaskWhatsApp(string whatsApp)
        {
            var digits = new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());
            var lastFour = digits.Length <= 4 ? digits : digits[^4..];
            return $"******{lastFour}";
        }

        private Task<Users> GetLoginUserAsync(string usernameOrWhatsApp)
        {
            var identifier = usernameOrWhatsApp?.Trim() ?? string.Empty;
            return identifier.Length == 10 && identifier.All(char.IsDigit)
                ? _userRepository.GetByWhatsAppLastTenDigitsAsync(identifier)
                : _userRepository.GetByUsernameAsync(identifier);
        }

        [HttpPost("SendResetPin")]
        public async Task<object> SendResetPin([FromBody] NewPassword model)
        {
            try
            {
                return await _accountServices.SendResetPinAsync(model.WhatsApp);
            }
            catch (Exception ex)
            {
                return new { Ok = false, Message = ex.Message };
            }
        }

        [HttpPost("ResetPassword")]
        public async Task<object> ResetPassword([FromBody] NewPassword model)
        {
            return await _accountServices.ResetPasswordAsync(model);
        }

    }

    public class ThemePreferenceRequest
    {
        [Required]
        public bool? IsDark { get; set; }
    }

    public class LoginRequest
    {
        [Required, StringLength(20, MinimumLength = 8)]
        public string Username { get; set; }

        [Required, StringLength(128, MinimumLength = 8)]
        public string Password { get; set; }
        public string Device { get; set; }
    }

    public class LoginConfirmationRequest : LoginRequest
    {
        public string Pin { get; set; }
    }
}
