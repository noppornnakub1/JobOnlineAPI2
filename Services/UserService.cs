using JobOnlineAPI.Models;
using JobOnlineAPI.Repositories;

namespace JobOnlineAPI.Services
{
    public class UserService(IAdminRepository adminRepository, ILdapService ldapService) : IUserService
    {
        private readonly IAdminRepository _adminRepository = adminRepository;
        private readonly ILdapService _ldapService = ldapService;

        public async Task<AdminUser?> AuthenticateAsync(string username, string password)
        {
            var user = await _adminRepository.GetUserByEmailAsync(username);

            if (user != null)
            {
                bool isPasswordMatched;
                if (user.PasswordHash.StartsWith("$2"))
                {
                    isPasswordMatched = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                else
                {
                    isPasswordMatched = _adminRepository.VerifySHA256Hash(password, user.PasswordHash);
                }

                if (isPasswordMatched)
                {
                    return new AdminUser
                    {
                        Username = user.Email,
                        Password = user.PasswordHash,
                        Role = "User",
                        UserId = user.UserId,
                        ConfirmConsent = user.ConfirmConsent
                    };
                }
            }

            var isLdapAuthenticated = await _ldapService.Authenticate(username, password);
            if (isLdapAuthenticated)
            {
                return new AdminUser
                {
                    AdminID = 0,
                    Username = username,
                    Password = "LDAPAuthenticated",
                    Role = "LDAP User"
                };
            }

            return null;
        }

        public async Task<string?> GetConfigValueAsync(string key)
        {
            return await _adminRepository.GetConfigValueAsync(key);
        }

        public async Task<string?> GetStyleValueAsync(string key)
        {
            return await _adminRepository.GetStyleValueAsync(key);
        }
    }
}