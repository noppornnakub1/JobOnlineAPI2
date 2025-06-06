namespace JobOnlineAPI.Services
{
    public interface IConsentService
    {
        Task<string?> GetUserConsentAsync(int userId);
        Task UpdateUserConsentAsync(int userId, string consent);
    }
}