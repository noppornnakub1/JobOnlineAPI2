using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;

namespace JobOnlineAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrefixController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PrefixController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetActivePrefixes")]
        public async Task<IActionResult> GetActivePrefixes()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                return StatusCode(500, new { message = "Connection string is missing or not configured." });
            }

            DataTable resultTable = new();

            try
            {
                using (SqlConnection connection = new(connectionString))
                {
                    await connection.OpenAsync();

                    using SqlCommand command = new("GetActivePrefixes", connection);
                    command.CommandType = CommandType.StoredProcedure;

                    using SqlDataAdapter adapter = new(command);
                    adapter.Fill(resultTable);
                }

                var resultList = ConvertDataTableToList(resultTable);
                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving data.", error = ex.Message });
            }
        }

        private static List<Dictionary<string, object>> ConvertDataTableToList(DataTable dataTable)
        {
            var list = new List<Dictionary<string, object>>();

            foreach (DataRow row in dataTable.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    dict[col.ColumnName] = row[col];
                }
                list.Add(dict);
            }

            return list;
        }
    }
}