using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.Services;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController(IUserService userService, IJwtTokenService jwtTokenService) : ControllerBase
    {
        private readonly IUserService _userService = userService;
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            try
            {
                var adminUser = await _userService.AuthenticateAsync(loginRequest.Username, loginRequest.Password);

                if (adminUser != null)
                {
                    if (!adminUser.UserId.HasValue)
                    {
                        return BadRequest(new { message = "UserId is missing for the authenticated user." });
                    }

                    var userModel = new UserModel
                    {
                        Username = adminUser.Username,
                        Role = adminUser.Role,
                        ConfirmConsent = adminUser.ConfirmConsent ?? string.Empty,
                        UserId = adminUser.UserId.Value
                    };

                    var token = _jwtTokenService.GenerateJwtToken(userModel);

                    return Ok(new { Token = token, userModel.Username, userModel.Role, userModel.ConfirmConsent, userModel.UserId });
                }

                return Unauthorized("Invalid username or password.");
            }
            catch (Exception)
            {
                return StatusCode(500, "An unexpected error occurred. Please try again later.");
            }
        }
    }

    public class LoginRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class UserModel
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string ConfirmConsent { get; set; } = string.Empty;
    }
}