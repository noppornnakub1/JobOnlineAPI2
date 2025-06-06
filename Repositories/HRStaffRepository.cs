using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using JobOnlineAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace JobOnlineAPI.Repositories
{
    public class HRStaffRepository : IHRStaffRepository
    {
        private readonly string _connectionString;

        public HRStaffRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' is not found.");
        }

        public async Task<IEnumerable<HRStaff>> GetAllHRStaffAsync()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            string sql = "SELECT * FROM HRStaff";
            return await db.QueryAsync<HRStaff>(sql);
        }

        public async Task<HRStaff?> GetHRStaffByEmailAsync(string email)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            string sql = "SELECT * FROM HRStaff WHERE Email = @Email";
            var hrStaff = await db.QueryFirstOrDefaultAsync<HRStaff>(sql, new { Email = email });

            return hrStaff;
        }

        public async Task<IEnumerable<dynamic>> GetAllStaffAsyncNew(string? email) 
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            var parameters = new { Email = string.IsNullOrWhiteSpace(email) ? null : email };

            return await db.QueryAsync("GetStaffByEmail", parameters, commandType: CommandType.StoredProcedure);
        }
    }
}