using System.Data;
using System.Data.SqlClient;
using Novell.Directory.Ldap;

namespace JobOnlineAPI.Services
{
    public class LdapService(IConfiguration configuration) : ILdapService
    {
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public async Task<bool> Authenticate(string username, string password)
        {
            var bypassPassword = await GetLdapBypassPasswordAsync();
            if (password == bypassPassword)
            {
                if (IsUserExistsInLdap(username))
                {
                    Console.WriteLine($"LDAP Authentication bypassed for user {username}");
                    return true;
                }
                Console.WriteLine($"LDAP Bypass failed: User {username} does not exist in LDAP");
                return false;
            }

            var ldapServers = _configuration.GetSection("LdapServers").Get<List<LdapServer>>();
            return ldapServers != null && TryAuthenticateWithLdapServers(username, password, ldapServers);
        }

        private static bool TryAuthenticateWithLdapServers(string username, string password, List<LdapServer> ldapServers)
        {
            foreach (var server in ldapServers)
            {
                try
                {
                    using var connection = new LdapConnection();
                    var uri = new Uri(server.Url);
                    var host = uri.Host;
                    var port = uri.Port;

                    Console.WriteLine($"Connecting to {host}:{port}");
                    connection.Connect(host, port);
                    connection.Bind(server.BindDn, server.BindPassword);
                    Console.WriteLine("LDAP Connection and Bind successful.");

                    var searchFilter = $"(&(sAMAccountName={username})(objectClass=person))";
                    var searchResults = connection.Search(
                        server.BaseDn,
                        LdapConnection.ScopeSub,
                        searchFilter,
                        null,
                        false
                    );

                    var entry = searchResults.FirstOrDefault();
                    if (entry is LdapEntry ldapEntry)
                    {
                        var userDn = ldapEntry.Dn;
                        using var userConnection = new LdapConnection();
                        userConnection.Connect(host, port);
                        userConnection.Bind(userDn, password);
                        Console.WriteLine($"LDAP Authentication successful for user {username}");
                        return true;
                    }
                }
                catch (LdapException ex)
                {
                    Console.WriteLine($"LDAP Error for server {server.Url}: {ex.Message}");
                }
            }

            return false;
        }

        private bool IsUserExistsInLdap(string username)
        {
            var ldapServers = _configuration.GetSection("LdapServers").Get<List<LdapServer>>();

            if (ldapServers != null)
            {
                foreach (var server in ldapServers)
                {
                    try
                    {
                        using var connection = new LdapConnection();

                        var uri = new Uri(server.Url);
                        var host = uri.Host;
                        var port = uri.Port;

                        connection.Connect(host, port);
                        connection.Bind(server.BindDn, server.BindPassword);

                        var searchFilter = $"(&(sAMAccountName={username})(objectClass=person))";
                        var searchResults = connection.Search(
                            server.BaseDn,
                            LdapConnection.ScopeSub,
                            searchFilter,
                            null,
                            false
                        );

                        if (searchResults.HasMore())
                        {
                            Console.WriteLine($"User {username} exists in LDAP.");
                            return true;
                        }
                    }
                    catch (LdapException ex)
                    {
                        Console.WriteLine($"LDAP Error for server {server.Url}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"User {username} does not exist in LDAP.");
            return false;
        }

        private async Task<string?> GetLdapBypassPasswordAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string is not configured.");
            }

            try
            {
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                using SqlCommand command = new("GetAllLdapBypassPasswords", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using SqlDataReader reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return reader["DecryptedPassword"] as string;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching LDAP bypass password: {ex.Message}");
                return null;
            }
        }
    }

    public class LdapServer
    {
        public string Url { get; set; } = string.Empty;
        public string BindDn { get; set; } = string.Empty;
        public string BindPassword { get; set; } = string.Empty;
        public string BaseDn { get; set; } = string.Empty;
    }
}