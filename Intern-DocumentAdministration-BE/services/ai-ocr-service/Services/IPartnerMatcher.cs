using AiOcrService.DTOs;
using System;
using System.Collections.Generic;

namespace AiOcrService.Services
{
    public interface IPartnerMatcher
    {
        (Guid? PartnerId, double Confidence) MatchPartner(string extractedText, List<PartnerDto> partners);
    }
}