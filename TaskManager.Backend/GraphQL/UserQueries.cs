using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate;
using TaskManager.Backend.Models;
using TaskManager.Backend.Services;

namespace TaskManager.Backend.GraphQL
{
    public class UserQueries
    {
        public async Task<IEnumerable<User>> GetUsers([Service] IUserService userService)
        {
            return await userService.GetAllUsersAsync();
        }

        public async Task<User?> GetUserById(System.Guid id, [Service] IUserService userService)
        {
            return await userService.GetUserByIdAsync(id);
        }

        public async Task<User?> GetUserByEmail(string email, [Service] IUserService userService)
        {
            return await userService.GetUserByEmailAsync(email);
        }
    }
}
