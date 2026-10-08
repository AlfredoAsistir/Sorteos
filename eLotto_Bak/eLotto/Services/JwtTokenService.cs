using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using eLotto.Core.Services;

namespace eLotto.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(int userId, string username, IEnumerable<string> roles, Guid sessionId)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var keyValue = jwtSettings["Key"]
                ?? throw new InvalidOperationException("JwtSettings:Key is not configured.");

            var tokenLifetimeMinutes = int.TryParse(_configuration["Authentication:TokenLifetimeMinutes"], out var minutes) && minutes > 0 ? minutes : 60;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, username),
                new(JwtRegisteredClaimNames.Sub, username),
                new(JwtRegisteredClaimNames.Jti, sessionId.ToString("D"))
            };

            claims.AddRange(
                roles.Where(role => !string.IsNullOrWhiteSpace(role))
                     .Select(role => new Claim(ClaimTypes.Role, role)));

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: ApplicationClock.NowOffset.AddMinutes(tokenLifetimeMinutes).UtcDateTime,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
