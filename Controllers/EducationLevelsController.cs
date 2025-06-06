using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EducationLevelsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public EducationLevelsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllEducationLevels()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using IDbConnection db = new SqlConnection(connectionString);
                var educationLevels = await db.QueryAsync<dynamic>(
                    "GetAllEducationLevels",
                    commandType: CommandType.StoredProcedure);

                return Ok(educationLevels);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "An error occurred while retrieving education levels.");
            }
        }
    }
}