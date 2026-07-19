using System;

namespace DAS.Api.Models
{
    public class Department
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public Guid? ManagerId { get; set; }
        public Guid? SecretaryId { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
