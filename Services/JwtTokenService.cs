using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobOnlineAPI.Controllers;

namespace JobOnlineAPI.Services
{
    public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
    {
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public string GenerateJwtToken(UserAdminModel user)
        {
            return GenerateToken(user.Username, user.Role);
        }

        public string GenerateJwtToken(UserModel user)
        {
            return GenerateToken(user.Username, user.Role);
        }

        private string GenerateToken(string username, string role)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
            var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");

            var keyBytes = Encoding.UTF8.GetBytes(GetJwtSecret());
            var key = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<JwtSecurityToken> ValidateTokenAsync(string token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
            var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");

            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.UTF8.GetBytes(GetJwtSecret());

            var validatedToken = await Task.Run(() =>
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken securityToken);

                return securityToken;
            });

            return (JwtSecurityToken)validatedToken;
        }

        private string GetJwtSecret()
        {
            var jwtSecret = _configuration["JwtSettings:AccessSecret"]
                ?? throw new InvalidOperationException("JwtSettings:AccessSecret is not configured.");

            if (Encoding.UTF8.GetBytes(jwtSecret).Length < 32)
            {
                throw new InvalidOperationException("JWT secret key must be at least 32 bytes long for HMAC-SHA256.");
            }

            return jwtSecret;
        }
    }
}