using Dapper;
using System.Data;
using System.Data.SqlClient;

namespace JobOnlineAPI.Services
{
    public class LocationService(IConfiguration configuration) : ILocationService
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("DefaultConnection is not configured in appsettings.json.");

        public async Task<IEnumerable<dynamic>> GetProvincesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync("GetProvinces", commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<dynamic>> GetDistrictsByProvinceAsync(int provinceCode)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync(
                "GetDistrictsByProvince",
                new { ProvinceCode = provinceCode },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<dynamic>> GetSubDistrictsByDistrictAsync(int districtId)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync(
                "GetSubDistrictsByDistrict",
                new { DistrictID = districtId },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<string?> GetPostalCodeAsync(int provinceCode, int districtCode, int subDistrictCode)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<string?>(
                "GetPostalCode",
                new
                {
                    ProvinceCode = provinceCode,
                    DistrictCode = districtCode,
                    SubDistrictCode = subDistrictCode
                },
                commandType: CommandType.StoredProcedure
            );
        }

        public static Task<IEnumerable<dynamic>> GetDistrictsAsync()
        {
            throw new NotImplementedException();
        }

        public static Task<IEnumerable<dynamic>> GetSubDistrictsAsync()
        {
            throw new NotImplementedException();
        }
    }
}