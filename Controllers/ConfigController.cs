using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.Services;
using System.Threading.Tasks;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly IUserService _userService;

        public ConfigController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetConfigValue(string key)
        {
            var configValue = await _userService.GetConfigValueAsync(key);
            if (configValue != null)
            {
                return Ok(configValue);
            }
            else
            {
                return NotFound($"Config value for key '{key}' not found.");
            }
        }
    }
}