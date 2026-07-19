using System;
using System.Threading.Tasks;
using HotChocolate;
using TaskManager.Backend.Models;
using TaskManager.Backend.Services;

namespace TaskManager.Backend.GraphQL
{
    public class UserMutations
    {
        // Local registration
        public async Task<User> RegisterUser(
            string email,
            string name,
            string password,
            Role role,
            string? department,
            [Service] IUserService userService)
        {
            return await userService.RegisterLocalUserAsync(email, name, password, role, department);
        }

        // Azure AD / OIDC user provisioning
        public async Task<User> SyncExternalUser(
            string email,
            string name,
            AuthProvider provider,
            string externalId,
            string? department,
            [Service] IUserService userService)
        {
            if (provider == AuthProvider.Local)
            {
                throw new Exception("Provider must be AzureAD or LDAP.");
            }
            return await userService.ProvisionExternalUserAsync(email, name, provider, externalId, department);
        }

        // LDAP login and provisioning
        public async Task<User?> AuthenticateLdapUser(
            string username,
            string password,
            [Service] ILDAPService ldapService,
            [Service] IUserService userService)
        {
            bool isAuthenticated = await ldapService.AuthenticateAsync(username, password);
            if (!isAuthenticated)
            {
                throw new Exception("LDAP authentication failed.");
            }

            string email = $"{username}@company.com";
            string name = username;
            string externalId = $"CN={username},OU=Users,DC=company,DC=com";

            return await userService.ProvisionExternalUserAsync(email, name, AuthProvider.LDAP, externalId, "IT");
        }
    }
}
