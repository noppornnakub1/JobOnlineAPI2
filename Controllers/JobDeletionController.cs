using Dapper;
using Microsoft.AspNetCore.Mvc;
using JobOnlineAPI.Models;
using JobOnlineAPI.DAL;
using JobOnlineAPI.Filters;
using System.Data;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobDeletionController(DapperContext context) : ControllerBase
    {
        private readonly DapperContext _context = context;

        [HttpDelete("deleteJob/{id}")]
        [TypeFilter(typeof(JwtAuthorizeAttribute))]
        public async Task<IActionResult> DeleteJobByJobID(int id)
        {
            try
            {
                using var connection = _context.CreateConnection();
                var parameters = new DynamicParameters();
                parameters.Add("@JobID", id);

                var remainingJobs = await connection.QueryAsync<Job>(
                    "sp_DeleteJobByJobID",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return Ok(new
                {
                    Message = "Job deleted successfully.",
                    RemainingJobs = remainingJobs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to delete job", ex.Message });
            }
        }
    }
}