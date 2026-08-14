using System;

namespace AiOcrService.DTOs
{
    public class PartnerDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ShortName { get; set; }
    }
}