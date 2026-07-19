using System;

namespace TaskManager.Backend.Models
{
    public enum Role
    {
        Admin,
        Manager,
        TeamLead,
        Employee,
        Guest
    }

    public enum AuthProvider
    {
        Local,
        AzureAD,
        LDAP
    }

    public enum UserStatus
    {
        Active,
        Deactivated
    }

    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public Role Role { get; set; } = Role.Employee;
        public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;
        public string? ExternalId { get; set; }
        public string? Department { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
