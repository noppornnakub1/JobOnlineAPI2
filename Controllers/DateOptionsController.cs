using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;

namespace JobOnlineAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DateOptionsController : ControllerBase
    {
        private readonly IDbConnection _dbConnection;

        public DateOptionsController(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpGet("GetDateOptions")]
        public async Task<IActionResult> GetDateOptions([FromQuery] int? currentYear)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@CurrentYear", currentYear);

                var dateOptions = await _dbConnection.QueryAsync<dynamic>(
                    "GetDateSelectionOptions",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return Ok(dateOptions);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = "An error occurred while retrieving date options.",
                    Error = ex.Message
                });
            }
        }
    }
}