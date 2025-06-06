using Microsoft.Data.SqlClient;
using System.Data;

namespace JobOnlineAPI.DAL
{
    public class DapperContext
    {
        private readonly string _connectionString;

        public DapperContext(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' is missing in configuration.");
        }

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }

    public class DapperContextHrms
    {
        private readonly string _connectionString;

        public DapperContextHrms(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _connectionString = configuration.GetConnectionString("DefaultConnectionHRMS")
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnectionHRMS' is missing in configuration.");
        }

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}