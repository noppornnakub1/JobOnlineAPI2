namespace JobOnlineAPI.Services
{
    public interface ILdapService
    {
        Task<bool> Authenticate(string username, string password);
    }
}