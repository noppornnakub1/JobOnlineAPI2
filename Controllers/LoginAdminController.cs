using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.Services;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginAdminController(IUserService userService, IJwtTokenService jwtTokenService) : ControllerBase
    {
        private readonly IUserService _userService = userService;
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginAdminRequest loginAdminRequest)
        {
            try
            {
                var adminUser = await _userService.AuthenticateAsync(loginAdminRequest.Username, loginAdminRequest.Password);

                if (adminUser != null)
                {
                    var userAdminModel = new UserAdminModel
                    {
                        Username = adminUser.Username,
                        Role = adminUser.Role
                    };

                    var token = _jwtTokenService.GenerateJwtToken(userAdminModel);

                    return Ok(new { Token = token, userAdminModel.Username, userAdminModel.Role });
                }

                return Unauthorized("Invalid username or password.");
            }
            catch (Exception)
            {
                return StatusCode(500, "An unexpected error occurred. Please try again later.");
            }
        }
    }

    public class LoginAdminRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class UserAdminModel
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}