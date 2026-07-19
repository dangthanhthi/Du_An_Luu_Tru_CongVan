using System;
using System.Threading.Tasks;
using Novell.Directory.Ldap;

namespace TaskManager.Backend.Services
{
    public interface ILDAPService
    {
        Task<bool> AuthenticateAsync(string username, string password);
    }

    public class LDAPService : ILDAPService
    {
        public Task<bool> AuthenticateAsync(string username, string password)
        {
            // This is a prototype. To connect to a real LDAP server, uncomment this block:
            /*
            try
            {
                using (var connection = new LdapConnection())
                {
                    connection.Connect("ldap.company.com", 389);
                    connection.Bind($"{username}@company.com", password);
                    return Task.FromResult(connection.Bound);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LDAP auth failed: {ex.Message}");
                return Task.FromResult(false);
            }
            */

            // Mock behavior for prototyping: accept login if password is 'ldap123'
            if (password == "ldap123")
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
