using Dapper;
using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.DAL;
using System.Data;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobDepartmentController(DapperContext context) : ControllerBase
    {
        private readonly DapperContext _context = context;

        [HttpGet("GetJobsDepartment")]
        public async Task<IActionResult> GetJobsDepartment()
        {
            try
            {
                using var connection = _context.CreateConnection();
                var result = await connection.QueryAsync(
                    "sp_GetJobsDepartment",
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}