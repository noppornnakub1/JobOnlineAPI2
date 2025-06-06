using JobOnlineAPI.Models;

namespace JobOnlineAPI.Services
{
    public interface IUserService
    {
        Task<AdminUser?> AuthenticateAsync(string username, string password);
        Task<string?> GetConfigValueAsync(string key);
        Task<string?> GetStyleValueAsync(string key);
    }
}
