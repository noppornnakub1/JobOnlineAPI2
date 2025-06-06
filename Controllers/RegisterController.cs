using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace JobOnlineAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegisterController : ControllerBase
    {
        private readonly IDbConnection _dbConnection;

        public RegisterController(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] Dictionary<string, object> data)
        {
            if (!data.TryGetValue("Email", out _) || !data.TryGetValue("Password", out object? value))
            {
                return BadRequest("Email and Password are required.");
            }
            string email = data["Email"]?.ToString() ?? string.Empty;
            string password = value?.ToString() ?? string.Empty;

            var passwordHash = HashPassword(password);

            var parameters = new DynamicParameters();
            parameters.Add("Email", email);
            parameters.Add("PasswordHash", passwordHash);
            parameters.Add("ResultCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("ResultMessage", dbType: DbType.String, size: 255, direction: ParameterDirection.Output);

            await _dbConnection.ExecuteAsync(
                "RegisterUser",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            int resultCode = parameters.Get<int>("ResultCode");
            string resultMessage = parameters.Get<string>("ResultMessage");

            if (resultCode == 1)
            {
                return Ok(resultMessage);
            }
            else if (resultCode == -1)
            {
                return BadRequest(resultMessage);
            }
            else
            {
                return StatusCode(500, resultMessage);
            }
        }

        private static string HashPassword(string password)
        {
            var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        }
    }
}