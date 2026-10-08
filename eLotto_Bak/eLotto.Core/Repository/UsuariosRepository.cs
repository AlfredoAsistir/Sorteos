using Microsoft.EntityFrameworkCore;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.Data.SqlClient;

namespace eLotto.Core.Repository
{
    public interface IUserRepository
    {
        Task<Users> GetByUsernameAsync(string username);
        Task<Users> GetByWhatsAppAsync(string whatsApp);
        Task<Users> GetByWhatsAppLastTenDigitsAsync(string whatsAppLastTenDigits);
        Task<int?> GetUserIdByReferralCodeAsync(string referralCode);
        Task<string> GetReferralCodeAsync(int userId);
        Task<UserRolsDto> GetUserRoleAsync(int userId);
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> WhatsAppExistsAsync(string whatsApp);
        Task<bool> CreateWithRoleAsync(Users user, string roleName, DateTime roleExpiration);
        Task<bool> ActivateByPinAsync(int userId, string pin);
        Task<bool> SetConfirmationCodeAsync(int userId, string confirmationCode);
        Task<bool> SetPasswordAsync(int userId, string passwordHash, string confirmationCode);
        Task<bool> SetDeviceAsync(int userId, bool isIos);
        Task<bool> SetActiveSessionIdAsync(int userId, Guid sessionId);
        Task<bool> ClearActiveSessionIdAsync(int userId, Guid sessionId);
        Task<Guid?> GetActiveSessionIdAsync(int userId);
        Task<bool?> GetThemeAsync(int userId);
        Task<bool> SetThemeAsync(int userId, bool isDark);
    }

    public class UserRepository : IUserRepository
    {
        private readonly eLottoContext _context;
        private readonly IReferralCodeGenerator _referralCodeGenerator;

        public UserRepository(eLottoContext context)
            : this(context, new ReferralCodeGenerator())
        {
        }

        public UserRepository(
            eLottoContext context,
            IReferralCodeGenerator referralCodeGenerator)
        {
            _context = context;
            _referralCodeGenerator = referralCodeGenerator;
        }

        public Task<Users> GetByUsernameAsync(string username) =>
            _context.Users.FirstOrDefaultAsync(user => user.User == username);

        public Task<Users> GetByWhatsAppAsync(string whatsApp) =>
            _context.Users.FirstOrDefaultAsync(user => user.WhatsApp == whatsApp);

        public Task<Users> GetByWhatsAppLastTenDigitsAsync(string whatsAppLastTenDigits) =>
            _context.Users.FirstOrDefaultAsync(user => user.WhatsApp.EndsWith(whatsAppLastTenDigits));

        public Task<int?> GetUserIdByReferralCodeAsync(string referralCode) =>
            _context.Users.AsNoTracking()
                .Where(user => user.ReferralCode == referralCode)
                .Select(user => (int?)user.Id)
                .SingleOrDefaultAsync();

        public Task<string> GetReferralCodeAsync(int userId) =>
            _context.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.ReferralCode)
                .SingleOrDefaultAsync();

        public Task<bool> UsernameExistsAsync(string username) =>
            _context.Users.AnyAsync(user => user.User.ToLower() == username.ToLower());

        public Task<bool> WhatsAppExistsAsync(string whatsApp) =>
            _context.Users.AnyAsync(user => user.WhatsApp == whatsApp);

        public async Task<UserRolsDto> GetUserRoleAsync(int userId)
        {
            return await (
                from userRole in _context.UserRols.AsNoTracking()
                join role in _context.Rol.AsNoTracking() on userRole.RolId equals role.Id
                where userRole.UserId == userId
                orderby userRole.Expire
                select new UserRolsDto
                {
                    UserId = userRole.UserId,
                    RolId = role.Id,
                    RolName = role.Name,
                    Expire = userRole.Expire
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> CreateWithRoleAsync(Users user, string roleName, DateTime roleExpiration)
        {
            var role = await _context.Rol
                .FirstOrDefaultAsync(existingRole => existingRole.Name.ToLower() == roleName.ToLower());
            if (role == null)
                return false;

            const int maximumAttempts = 10;
            user.ReferralCode = NormalizeOrGenerateReferralCode(user.ReferralCode);

            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.Users.AddAsync(user);
                    await _context.SaveChangesAsync();
                    await _context.UserRols.AddAsync(new UserRols
                    {
                        UserId = user.Id,
                        RolId = role.Id,
                        Expire = roleExpiration
                    });
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateException exception) when (IsReferralCodeCollision(exception))
                {
                    await transaction.RollbackAsync();
                    _context.Entry(user).State = EntityState.Detached;
                    user.Id = 0;

                    if (attempt == maximumAttempts)
                    {
                        throw new InvalidOperationException(
                            "No fue posible generar un código de referido único después de varios intentos.",
                            exception);
                    }

                    user.ReferralCode = _referralCodeGenerator.Generate();
                }
            }

            throw new InvalidOperationException("No fue posible crear el usuario.");
        }

        private string NormalizeOrGenerateReferralCode(string referralCode)
        {
            var normalized = referralCode?.Trim().ToUpperInvariant();
            return ReferralCodeGenerator.IsValid(normalized)
                ? normalized
                : _referralCodeGenerator.Generate();
        }

        private static bool IsReferralCodeCollision(DbUpdateException exception)
        {
            var sqlException = exception.InnerException as SqlException;
            return sqlException?.Number is 2601 or 2627 &&
                sqlException.Message.Contains(
                    "UX_Users_ReferralCode",
                    StringComparison.Ordinal);
        }

        public async Task<bool> ActivateByPinAsync(int userId, string pin)
        {
            var user = await _context.Users.FirstOrDefaultAsync(existingUser =>
                existingUser.Id == userId && existingUser.ConfirmCode == pin);
            if (user == null)
                return false;

            user.IsActive = true;
            user.ConfirmCode = string.Empty;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetConfirmationCodeAsync(int userId, string confirmationCode)
        {
            var user = await _context.Users.FirstOrDefaultAsync(existingUser => existingUser.Id == userId);
            if (user == null)
                return false;

            user.ConfirmCode = confirmationCode;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetPasswordAsync(int userId, string passwordHash, string confirmationCode)
        {
            var user = await _context.Users.FirstOrDefaultAsync(existingUser => existingUser.Id == userId);
            if (user == null || string.IsNullOrWhiteSpace(confirmationCode) || user.ConfirmCode != confirmationCode)
                return false;

            user.Password = passwordHash;
            user.IsActive = true;
            user.ConfirmCode = string.Empty;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetDeviceAsync(int userId, bool isIos)
        {
            var user = await _context.Users.FirstOrDefaultAsync(existingUser => existingUser.Id == userId);
            if (user == null)
                return false;

            if (isIos)
                user.IsIos = true;
            else
                user.IsAndroid = true;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetActiveSessionIdAsync(int userId, Guid sessionId)
        {
            var updated = await _context.Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.ActiveSessionId, sessionId));

            return updated == 1;
        }

        public Task<Guid?> GetActiveSessionIdAsync(int userId) =>
            _context.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.ActiveSessionId)
                .FirstOrDefaultAsync();

        public Task<bool?> GetThemeAsync(int userId) =>
            _context.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (bool?)user.IsDark)
                .FirstOrDefaultAsync();

        public async Task<bool> SetThemeAsync(int userId, bool isDark)
        {
            var updated = await _context.Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsDark, isDark));

            return updated == 1;
        }

        public async Task<bool> ClearActiveSessionIdAsync(int userId, Guid sessionId)
        {
            var updated = await _context.Users
                .Where(user => user.Id == userId && user.ActiveSessionId == sessionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.ActiveSessionId, (Guid?)null));

            return updated == 1;
        }
    }
}
