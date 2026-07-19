using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskManager.Backend.Data;
using TaskManager.Backend.Models;
using BCrypt.Net;

namespace TaskManager.Backend.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User> RegisterLocalUserAsync(string email, string name, string password, Role role, string? department)
        {
            var existing = await GetUserByEmailAsync(email);
            if (existing != null)
            {
                throw new Exception($"User with email {email} already exists.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Email = email,
                Name = name,
                PasswordHash = passwordHash,
                Role = role,
                AuthProvider = AuthProvider.Local,
                Department = department,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> ProvisionExternalUserAsync(string email, string name, AuthProvider provider, string externalId, string? department)
        {
            var existing = await GetUserByEmailAsync(email);
            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.ExternalId) || existing.ExternalId != externalId)
                {
                    existing.ExternalId = externalId;
                    existing.AuthProvider = provider;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _context.Entry(existing).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                }
                return existing;
            }

            var user = new User
            {
                Email = email,
                Name = name,
                PasswordHash = null,
                Role = Role.Employee,
                AuthProvider = provider,
                ExternalId = externalId,
                Department = department,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}
