using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;
using JobOnlineAPI.Services;
using Microsoft.IdentityModel.Tokens;

namespace JobOnlineAPI.Filters
{
    [AttributeUsage(AttributeTargets.All)]
    public class JwtAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var jwtTokenService = context.HttpContext.RequestServices.GetRequiredService<IJwtTokenService>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtAuthorizeAttribute>>();

            string? authHeader = context.HttpContext.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(authHeader))
            {
                logger.LogWarning("AccessToken cannot be empty");
                context.Result = new BadRequestObjectResult(new { message = "AccessToken cannot be empty" });
                return;
            }

            string token = authHeader.StartsWith("Bearer ") ? authHeader[7..].Trim() : authHeader;
            if (token.Count(c => c == '.') != 2)
            {
                context.Result = new BadRequestObjectResult(new { message = "Invalid token format. Expected JWS format (header.payload.signature)" });
                return;
            }

            try
            {
                var jwtToken = await jwtTokenService.ValidateTokenAsync(token);
                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrEmpty(subClaim))
                {
                    logger.LogWarning("No 'sub' claim found in the token");
                    context.Result = new BadRequestObjectResult(new { message = "Invalid token: No 'sub' claim found" });
                    return;
                }

                var parts = subClaim.Split('|');
                if (parts.Length != 2)
                {
                    logger.LogWarning("Invalid 'sub' claim format: {Sub}", subClaim);
                    context.Result = new BadRequestObjectResult(new { message = "Invalid token: 'sub' claim format is incorrect" });
                    return;
                }
            }
            catch (SecurityTokenMalformedException ex)
            {
                logger.LogWarning(ex, "Invalid token format: {Message}", ex.Message);
                context.Result = new BadRequestObjectResult(new { message = "Invalid token format: Token must be a valid JWT with correct segments" });
                return;
            }
            catch (SecurityTokenException ex)
            {
                logger.LogWarning(ex, "Token validation failed: {Message}", ex.Message);
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid or expired token", error = ex.Message });
                return;
            }

            await next();
        }
    }
}