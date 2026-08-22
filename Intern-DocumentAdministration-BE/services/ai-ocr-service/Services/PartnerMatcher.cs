using AiOcrService.DTOs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AiOcrService.Services
{
    public class PartnerMatcher : IPartnerMatcher
    {
        private static readonly HashSet<string> GenericEmailDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "icloud.com", "mail.com", "protonmail.com"
        };

        public MatchResult MatchPartner(string extractedText, List<PartnerDto> partners, string? senderEmail = null)
        {
            if (partners == null || !partners.Any())
            {
                return new MatchResult { PartnerId = null, Confidence = 0.0, MatchMethod = null };
            }

            var rawText = extractedText ?? string.Empty;
            var normalizedText = rawText.ToUpperInvariant();
            var unaccentedText = RemoveDiacritics(rawText).ToUpperInvariant();

            // -------------------------------------------------------------
            // TẦNG 1: SO KHỚP QUA SENDER EMAIL HOẶC EMAIL DOMAIN (Confidence 99% / 98%)
            // -------------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(senderEmail))
            {
                var cleanSender = senderEmail.Trim().ToLowerInvariant();

                // 1.1 Khớp chính xác địa chỉ email người gửi
                var directEmailMatch = partners.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.Email) && p.Email.Trim().ToLowerInvariant() == cleanSender);
                if (directEmailMatch != null)
                {
                    return new MatchResult
                    {
                        PartnerId = directEmailMatch.Id,
                        Confidence = 0.99,
                        MatchMethod = "DirectSenderEmail"
                    };
                }

                // 1.2 Khớp qua Email Domain của tổ chức (bỏ qua các public email provider như gmail/yahoo)
                var atIndex = cleanSender.IndexOf('@');
                if (atIndex >= 0 && atIndex < cleanSender.Length - 1)
                {
                    var senderDomain = cleanSender.Substring(atIndex + 1);
                    if (!GenericEmailDomains.Contains(senderDomain))
                    {
                        var domainMatch = partners.FirstOrDefault(p =>
                            !string.IsNullOrWhiteSpace(p.Email) &&
                            p.Email.Trim().ToLowerInvariant().EndsWith("@" + senderDomain));

                        if (domainMatch != null)
                        {
                            return new MatchResult
                            {
                                PartnerId = domainMatch.Id,
                                Confidence = 0.98,
                                MatchMethod = "EmailDomain"
                            };
                        }
                    }
                }
            }

            // -------------------------------------------------------------
            // TẦNG 2: SO KHỚP THEO MÃ SỐ THUẾ (TaxCode) TRONG VĂN BẢN (Confidence 98%)
            // -------------------------------------------------------------
            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.TaxCode)) continue;
                var taxCode = partner.TaxCode.Trim();

                if (taxCode.Length >= 8)
                {
                    var taxPattern = $@"\b{Regex.Escape(taxCode)}\b";
                    if (Regex.IsMatch(rawText, taxPattern) || normalizedText.Contains(taxCode.ToUpperInvariant()))
                    {
                        return new MatchResult
                        {
                            PartnerId = partner.Id,
                            Confidence = 0.98,
                            MatchMethod = "TaxCode"
                        };
                    }
                }
            }

            // -------------------------------------------------------------
            // TẦNG 3: SO KHỚP THEO EMAIL TRÍCH XUẤT TRONG NỘI DUNG TÀI LIỆU (Confidence 95%)
            // -------------------------------------------------------------
            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.Email)) continue;
                var partnerEmail = partner.Email.Trim().ToLowerInvariant();

                if (rawText.ToLowerInvariant().Contains(partnerEmail))
                {
                    return new MatchResult
                    {
                        PartnerId = partner.Id,
                        Confidence = 0.95,
                        MatchMethod = "DocumentBodyEmail"
                    };
                }
            }

            // -------------------------------------------------------------
            // TẦNG 4: SO KHỚP THEO TÊN VIẾT TẮT (ShortName) (Confidence 90% - 95%)
            // -------------------------------------------------------------
            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.ShortName)) continue;

                var cleanShortName = partner.ShortName.Trim().ToUpperInvariant();
                var unaccentedShortName = RemoveDiacritics(partner.ShortName.Trim()).ToUpperInvariant();

                // 4.1 ShortName dài (>= 4 ký tự) -> Contains
                if (cleanShortName.Length >= 4)
                {
                    if (normalizedText.Contains(cleanShortName) || unaccentedText.Contains(unaccentedShortName))
                    {
                        return new MatchResult
                        {
                            PartnerId = partner.Id,
                            Confidence = 0.95,
                            MatchMethod = "ShortName"
                        };
                    }
                }
                else if (cleanShortName.Length >= 2)
                {
                    // 4.2 ShortName ngắn (2-3 ký tự) -> dùng Regex word boundary
                    var pattern1 = $@"\b{Regex.Escape(cleanShortName)}\b";
                    var pattern2 = $@"\b{Regex.Escape(unaccentedShortName)}\b";

                    if (Regex.IsMatch(normalizedText, pattern1) || Regex.IsMatch(unaccentedText, pattern2))
                    {
                        return new MatchResult
                        {
                            PartnerId = partner.Id,
                            Confidence = 0.90,
                            MatchMethod = "ShortNameBoundary"
                        };
                    }
                }
            }

            // -------------------------------------------------------------
            // TẦNG 5: SO KHỚP THEO TÊN ĐẦY ĐỦ (FullName) (Confidence 85%)
            // -------------------------------------------------------------
            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.FullName)) continue;

                var cleanFullName = partner.FullName.Trim().ToUpperInvariant();
                var unaccentedFullName = RemoveDiacritics(partner.FullName.Trim()).ToUpperInvariant();

                if (cleanFullName.Length >= 4 && (normalizedText.Contains(cleanFullName) || unaccentedText.Contains(unaccentedFullName)))
                {
                    return new MatchResult
                    {
                        PartnerId = partner.Id,
                        Confidence = 0.85,
                        MatchMethod = "FullName"
                    };
                }
            }

            return new MatchResult { PartnerId = null, Confidence = 0.0, MatchMethod = null };
        }

        public string? ExtractReferenceNumber(string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText)) return null;

            // Regex các mẫu số hiệu công văn phổ biến trong văn bản hành chính Việt Nam & quốc tế
            // Mẫu 1: "Số: 128/BGDĐT-GDĐH" hoặc "Số : 45/UBND-VX" hoặc "Số: 01/2026/QĐ-UBND"
            var patterns = new[]
            {
                @"(?:Số|So|No|Số/No|Số\s*\/\s*No)\s*[:.]\s*([0-9A-ZĐa-z\/\-_]{2,40})",
                @"\b(?:Số|So)\s*([0-9]+\s*\/\s*[0-9A-ZĐa-z\-_]+(?:\s*\/\s*[0-9A-ZĐa-z\-_]+)?)",
                @"(?:Ref|Reference)\s*(?:No|Number)?\s*[:.]\s*([0-9A-Za-z\/\-_]{2,40})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(extractedText, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var result = match.Groups[1].Value.Trim();
                    // Loại bỏ các ký tự dấu câu thừa ở cuối nếu có
                    result = result.TrimEnd('.', ',', ';', ':', '-', ' ');
                    if (result.Length >= 2 && !result.Equals("ngay", StringComparison.OrdinalIgnoreCase))
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(normalizedString.Length);

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D');
        }
    }
}