using eLotto.Core.Models;
using eLotto.Core.Repository;
using Microsoft.Extensions.Configuration;

namespace eLotto.Core.Services
{
    public interface IAccountServices
    {
        Task<object> CreateUserAsync(UserCreate model);
        Task<bool> SendLoginConfirmationPinAsync(Users user);
        Task<bool> ConfirmLoginAsync(Users user, string pin);
        Task<object> ResetPasswordAsync(NewPassword model);
        Task<object> SendResetPinAsync(string whatsApp);
    }

    public class AccountServices : IAccountServices
    {
        private readonly IUserRepository _userRepository;
        private readonly IWhatsAppService _whatsAppService;
        private readonly string _applicationName;
        private readonly int _userRoleValidityYears;

        public AccountServices(
            IUserRepository userRepository,
            IWhatsAppService whatsAppService,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _whatsAppService = whatsAppService;
            _applicationName = configuration["Branding:ApplicationName"]?.Trim()
                ?? throw new InvalidOperationException("Branding:ApplicationName is not configured.");
            _userRoleValidityYears = int.TryParse(configuration["Authentication:UserRoleValidityYears"], out var years) && years > 0 ? years : 10;
        }

        public async Task<object> CreateUserAsync(UserCreate model)
        {
            if (!model.ConfirmedOver18)
                return new { Ok = false, Message = "Debes confirmar que eres mayor de 18 años para registrarte." };

            if (string.IsNullOrWhiteSpace(model.WhatsApp))
                return new { Ok = false, Message = "El número de WhatsApp es obligatorio." };

            var existingUser = await _userRepository.GetByWhatsAppAsync(model.WhatsApp);
            if (existingUser?.IsActive == true)
                return new { Ok = false, Message = "El número de WhatsApp ya se encuentra registrado." };
            if (existingUser != null)
                return new { Ok = false, Message = "El número de WhatsApp ya se encuentra registrado y está pendiente de confirmación. Inicia sesión para confirmar tu cuenta." };
            if (string.IsNullOrWhiteSpace(model.User) || model.User.Length < 8)
                return new { Ok = false, Message = "El usuario debe ser mínimo de 8 caracteres." };
            if (await _userRepository.UsernameExistsAsync(model.User))
                return new { Ok = false, Message = "El usuario ya está en uso, favor de asignar uno diferente." };
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
                return new { Ok = false, Message = "El password debe tener al menos 8 caracteres." };
            if (model.Password != model.ConfirmPassword)
                return new { Ok = false, Message = "El password y su confirmación no coinciden." };

            int? referredByUserId = null;
            if (!string.IsNullOrWhiteSpace(model.ReferralCode))
            {
                var normalizedReferralCode = model.ReferralCode.Trim().ToUpperInvariant();
                if (!ReferralCodeGenerator.IsValid(normalizedReferralCode))
                    return new { Ok = false, Message = "El código de referido no tiene un formato válido." };

                referredByUserId = await _userRepository.GetUserIdByReferralCodeAsync(normalizedReferralCode);
                if (!referredByUserId.HasValue)
                    return new { Ok = false, Message = "El código de referido no existe." };
            }

            var device = model.Device?.ToLowerInvariant() ?? string.Empty;
            var user = new Users
            {
                User = model.User,
                Name = model.Name,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                IsActive = false,
                Date = ApplicationClock.Now,
                Over18ConfirmedAt = ApplicationClock.Now,
                WhatsApp = model.WhatsApp,
                ConfirmCode = string.Empty,
                Languaje = "es",
                IsDark = true,
                IsIos = device == "ios",
                IsAndroid = device == "android",
                ReferredByUserId = referredByUserId
            };

            try
            {
                var created = await _userRepository.CreateWithRoleAsync(
                    user,
                    "User",
                    ApplicationClock.Now.AddYears(_userRoleValidityYears));
                if (!created)
                    return new { Ok = false, Message = "No se registró el usuario. Favor de informar a soporte: rol incorrecto." };

                var welcomeSent = false;
                try
                {
                    welcomeSent = await _whatsAppService.SendTextAsync(
                        user.WhatsApp,
                        $"👋 ¡Hola {user.Name}!\n\n🎉 Bienvenido a {_applicationName}. Tu cuenta fue creada correctamente.\n\n🔐 Inicia sesión y activa tu cuenta.\n\nTu usuario registrado es {user.User}\n\n🍀 ¡Mucha suerte en tus próximos sorteos!");
                }
                catch
                {
                    // El mensaje de bienvenida es opcional y no revierte una cuenta ya creada.
                }

                return new
                {
                    Ok = true,
                    WelcomeSent = welcomeSent,
                    Message = "Cuenta creada correctamente. Continúa con el inicio de sesión para confirmar tu número de WhatsApp."
                };
            }
            catch
            {
                return new { Ok = false, Message = "No fue posible crear la cuenta." };
            }
        }

        public async Task<bool> SendLoginConfirmationPinAsync(Users user)
        {
            var pin = Random.Shared.Next(1000, 10000).ToString();
            await _userRepository.SetConfirmationCodeAsync(user.Id, pin);
            try
            {
                return await _whatsAppService.SendTextAsync(
                    user.WhatsApp,
                    $"🔐 Confirmación de cuenta {_applicationName}\n\nTu PIN de acceso es:\n\n✨ {pin} ✨\n\nIngresa estos 4 números en la ventana de confirmación.\n\nSi no solicitaste este código, puedes ignorar el mensaje.");
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> ConfirmLoginAsync(Users user, string pin)
        {
            return _userRepository.ActivateByPinAsync(user.Id, pin);
        }

        public async Task<object> SendResetPinAsync(string whatsApp)
        {
            if (!IsValidLocalWhatsApp(whatsApp))
                return new { Ok = false, Message = "Ingresa los 10 dígitos de tu WhatsApp, sin código de país." };

            var user = await _userRepository.GetByWhatsAppLastTenDigitsAsync(whatsApp);
            if (user == null)
                return new { Ok = false, Message = "WhatsApp no encontrado." };

            var pin = Random.Shared.Next(100000, 1000000).ToString();
            await _userRepository.SetConfirmationCodeAsync(user.Id, pin);
            var sent = await _whatsAppService.SendTextAsync(
                user.WhatsApp,
                $"Tu PIN de {_applicationName} para cambiar el password es: {pin}");

            return sent
                ? new { Ok = true, Message = "Enviamos el PIN a tu WhatsApp." }
                : new { Ok = false, Message = "No fue posible enviar el PIN por WhatsApp." };
        }

        public async Task<object> ResetPasswordAsync(NewPassword model)
        {
            if (!IsValidLocalWhatsApp(model.WhatsApp))
                return new { Ok = false, Message = "Ingresa los 10 dígitos de tu WhatsApp, sin código de país." };

            var user = await _userRepository.GetByWhatsAppLastTenDigitsAsync(model.WhatsApp);
            if (user == null)
                return new { Ok = false, Message = "WhatsApp no encontrado." };
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
                return new { Ok = false, Message = "El password debe tener al menos 8 caracteres." };
            if (string.IsNullOrWhiteSpace(model.ConfirmPassword) || model.ConfirmPassword.Length < 8)
                return new { Ok = false, Message = "La confirmación del password debe tener al menos 8 caracteres." };
            if (model.Password != model.ConfirmPassword)
                return new { Ok = false, Message = "El password y su confirmación no coinciden." };
            if (string.IsNullOrWhiteSpace(model.ConfirmCode) ||
                model.ConfirmCode.Length != 6 ||
                !model.ConfirmCode.All(char.IsDigit))
                return new { Ok = false, Message = "El PIN debe contener 6 números." };

            var updated = await _userRepository.SetPasswordAsync(
                user.Id,
                BCrypt.Net.BCrypt.HashPassword(model.Password),
                model.ConfirmCode);

            return updated
                ? new { Ok = true, Message = "El password se ha creado correctamente." }
                : new { Ok = false, Message = "PIN incorrecto." };
        }

        private static bool IsValidLocalWhatsApp(string whatsApp) =>
            !string.IsNullOrWhiteSpace(whatsApp) &&
            whatsApp.Length == 10 &&
            whatsApp.All(char.IsDigit);
    }
}
