using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.Services;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StylesController(IUserService userService) : ControllerBase
    {
        private readonly IUserService _userService = userService;

        [HttpGet("{key}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStyleValue(string key)
        {
            var styleValue = await _userService.GetStyleValueAsync(key);
            if (styleValue != null)
            {
                return Ok(styleValue);
            }
            else
            {
                return NotFound($"Style value for key '{key}' not found.");
            }
        }
    }
}