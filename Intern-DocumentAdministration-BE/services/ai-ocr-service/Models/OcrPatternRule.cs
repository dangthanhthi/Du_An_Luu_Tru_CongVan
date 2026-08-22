using System;

namespace AiOcrService.Models
{
    public class OcrPatternRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RuleType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public int Priority { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateOcrRuleRequest
    {
        public string RuleType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public int Priority { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
    }

    public class UpdateOcrRuleRequest
    {
        public string? RuleType { get; set; }
        public string? Name { get; set; }
        public string? Pattern { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
        public string? Description { get; set; }
    }

    public class TestPatternRequest
    {
        public string Pattern { get; set; } = string.Empty;
        public string SampleText { get; set; } = string.Empty;
        public string? RuleType { get; set; }
    }
}
