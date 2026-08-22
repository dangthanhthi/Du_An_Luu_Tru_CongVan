using AiOcrService.DTOs;
using System;
using System.Collections.Generic;

namespace AiOcrService.Services
{
    public class MatchResult
    {
        public Guid? PartnerId { get; set; }
        public double Confidence { get; set; }
        public string? MatchMethod { get; set; }
    }

    public interface IPartnerMatcher
    {
        MatchResult MatchPartner(string extractedText, List<PartnerDto> partners, string? senderEmail = null);
        string? ExtractReferenceNumber(string extractedText);
    }
}