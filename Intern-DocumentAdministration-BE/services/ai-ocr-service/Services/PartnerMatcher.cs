using AiOcrService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiOcrService.Services
{
    public class PartnerMatcher : IPartnerMatcher
    {
        public (Guid? PartnerId, double Confidence) MatchPartner(string extractedText, List<PartnerDto> partners)
        {
            if (string.IsNullOrWhiteSpace(extractedText) || partners == null || !partners.Any())
            {
                return (null, 0.0);
            }

            var normalizedText = extractedText.ToUpperInvariant();

            // 1. Ưu tiên khớp theo Tên Viết Tắt (ShortName)
            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.ShortName)) continue;
                
                var cleanShortName = partner.ShortName.Trim().ToUpperInvariant();
                
                // Nếu ShortName dài (>= 4 ký tự) -> dùng Contains
                if (cleanShortName.Length >= 4)
                {
                    if (normalizedText.Contains(cleanShortName))
                    {
                        return (partner.Id, 0.95); // 95% confidence khi khớp tên viết tắt dài
                    }
                }
                else if (cleanShortName.Length >= 2)
                {
                    // Nếu ShortName ngắn (2-3 ký tự, VD: "BGD", "BYT") -> dùng Regex ranh giới từ (\b) để tránh false positive
                    var pattern = $@"\b{Regex.Escape(cleanShortName)}\b";
                    if (Regex.IsMatch(normalizedText, pattern))
                    {
                        return (partner.Id, 0.90); // 90% confidence
                    }
                }
            }

            // 2. Nếu không khớp tên viết tắt, tìm theo Tên Đầy Đủ (FullName)
            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.FullName)) continue;

                var cleanFullName = partner.FullName.Trim().ToUpperInvariant();
                if (cleanFullName.Length >= 4 && normalizedText.Contains(cleanFullName))
                {
                    return (partner.Id, 0.85); // 85% confidence khi khớp tên đầy đủ
                }
            }

            return (null, 0.0);
        }
    }
}