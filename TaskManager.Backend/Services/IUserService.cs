using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskManager.Backend.Models;

namespace TaskManager.Backend.Services
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(Guid id);
        Task<User?> GetUserByEmailAsync(string email);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> RegisterLocalUserAsync(string email, string name, string password, Role role, string? department);
        Task<User> ProvisionExternalUserAsync(string email, string name, AuthProvider provider, string externalId, string? department);
    }
}
