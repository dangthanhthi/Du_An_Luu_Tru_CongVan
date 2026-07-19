using System;

namespace DAS.Api.Models
{
    public enum Role
    {
        Admin,
        Director,
        DirectorSecretary,
        DepartmentHead,
        DepartmentSecretary,
        Employee
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
        public string PasswordHash { get; set; } = string.Empty;
        public Role Role { get; set; } = Role.Employee;
        public Guid? DepartmentId { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
