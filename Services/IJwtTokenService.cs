using System.IdentityModel.Tokens.Jwt;
using JobOnlineAPI.Controllers;

namespace JobOnlineAPI.Services
{
    public interface IJwtTokenService
    {
        string GenerateJwtToken(UserAdminModel user);
        string GenerateJwtToken(UserModel user);
        Task<JwtSecurityToken> ValidateTokenAsync(string token);
    }
}