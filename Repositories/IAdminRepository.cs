using JobOnlineAPI.Models;

namespace JobOnlineAPI.Repositories
{
    public interface IAdminRepository
    {
        Task<int> AddAdminUserAsync(AdminUser admin);
        Task<bool> VerifyPasswordAsync(string username, string password);
        Task<AdminUser?> GetAdminUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<string?> GetConfigValueAsync(string key);
        Task<string?> GetStyleValueAsync(string key);
        bool VerifySHA256Hash(string input, string storedHash);
    }
}