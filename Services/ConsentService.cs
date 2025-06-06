using Dapper;
using System.Data;

namespace JobOnlineAPI.Services
{
    public class ConsentService(IDbConnection dbConnection) : IConsentService
    {
        private readonly IDbConnection _dbConnection = dbConnection;

        public async Task<string?> GetUserConsentAsync(int userId)
        {
            var query = "SELECT ConfirmConsent FROM Users WHERE UserId = @UserId";
            return await _dbConnection.QuerySingleOrDefaultAsync<string>(query, new { UserId = userId });
        }

        public async Task UpdateUserConsentAsync(int userId, string consent)
        {
            var query = "UPDATE Users SET ConfirmConsent = @Consent WHERE UserId = @UserId";
            await _dbConnection.ExecuteAsync(query, new { UserId = userId, Consent = consent });
        }
    }
}