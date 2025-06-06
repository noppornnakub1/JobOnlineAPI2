using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecureController : ControllerBase
    {
        [Authorize]
        [HttpGet("SecureEndpoint")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SecureEndpointResponse))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult SecureEndpoint()
        {
            return Ok(new SecureEndpointResponse
            {
                Message = "You have access to this secure endpoint!"
            });
        }
    }

    public class SecureEndpointResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}