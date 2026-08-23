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
            
            int totalLength = normalizedText.Length;
            int headerThreshold = (int)(totalLength * 0.3);
            string headerText = normalizedText.Substring(0, Math.Min(totalLength, headerThreshold));
            string unaccentedHeaderText = unaccentedText.Substring(0, Math.Min(totalLength, headerThreshold));
            int noiNhanIndex = unaccentedText.IndexOf("NOI NHAN");

            var matches = new List<MatchResult>();

            // -------------------------------------------------------------
            // TẦNG 1: SO KHỚP QUA SENDER EMAIL HOẶC EMAIL DOMAIN (Confidence 99% / 98%)
            // -------------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(senderEmail))
            {
                var cleanSender = senderEmail.Trim().ToLowerInvariant();

                var directEmailMatch = partners.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.Email) && p.Email.Trim().ToLowerInvariant() == cleanSender);
                if (directEmailMatch != null)
                {
                    matches.Add(new MatchResult
                    {
                        PartnerId = directEmailMatch.Id,
                        Confidence = 0.99,
                        MatchMethod = "DirectSenderEmail"
                    });
                }

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
                            matches.Add(new MatchResult
                            {
                                PartnerId = domainMatch.Id,
                                Confidence = 0.98,
                                MatchMethod = "EmailDomain"
                            });
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
                        matches.Add(new MatchResult
                        {
                            PartnerId = partner.Id,
                            Confidence = 0.98,
                            MatchMethod = "TaxCode"
                        });
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
                    matches.Add(new MatchResult
                    {
                        PartnerId = partner.Id,
                        Confidence = 0.95,
                        MatchMethod = "DocumentBodyEmail"
                    });
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

                var pattern1 = $@"\b{Regex.Escape(cleanShortName)}\b";
                var pattern2 = $@"\b{Regex.Escape(unaccentedShortName)}\b";

                Match m1 = Regex.Match(normalizedText, pattern1);
                Match m2 = Regex.Match(unaccentedText, pattern2);

                if (m1.Success || m2.Success)
                {
                    int matchIndex = m1.Success ? m1.Index : m2.Index;
                    double conf = cleanShortName.Length >= 4 ? 0.95 : 0.90;
                    
                    if (matchIndex < headerThreshold)
                    {
                        conf += 0.05;
                    }
                    if (noiNhanIndex >= 0 && matchIndex > noiNhanIndex)
                    {
                        conf -= 0.10;
                    }
                    
                    matches.Add(new MatchResult
                    {
                        PartnerId = partner.Id,
                        Confidence = Math.Min(0.99, conf),
                        MatchMethod = cleanShortName.Length >= 4 ? "ShortName" : "ShortNameBoundary"
                    });
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

                if (cleanFullName.Length >= 4)
                {
                    int matchIndex = normalizedText.IndexOf(cleanFullName);
                    if (matchIndex < 0)
                        matchIndex = unaccentedText.IndexOf(unaccentedFullName);

                    if (matchIndex >= 0)
                    {
                        double conf = 0.85;
                        if (matchIndex < headerThreshold) conf += 0.05;
                        if (noiNhanIndex >= 0 && matchIndex > noiNhanIndex) conf -= 0.10;
                        
                        matches.Add(new MatchResult
                        {
                            PartnerId = partner.Id,
                            Confidence = Math.Min(0.99, conf),
                            MatchMethod = "FullName"
                        });
                    }
                }
            }

            if (matches.Any())
            {
                return matches.OrderByDescending(m => m.Confidence).First();
            }

            return new MatchResult { PartnerId = null, Confidence = 0.0, MatchMethod = null };
        }

        public string? ExtractReferenceNumber(string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText)) return null;

            var patterns = new[]
            {
                @"(?:Số|So|No|Số/No|Số\s*\/\s*No)\s*[:.]\s*([0-9A-ZĐa-z\/\-_\s]{2,40})",
                @"\b(?:Số|So)\s*([0-9]+\s*\/\s*[0-9A-ZĐa-z\-_]+(?:\s*\/\s*[0-9A-ZĐa-z\-_]+)?)",
                @"(?:Ref|Reference)\s*(?:No|Number)?\s*[:.]\s*([0-9A-Za-z\/\-_\s]{2,40})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(extractedText, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var result = match.Groups[1].Value.Trim();
                    result = Regex.Replace(result, @"\s+", "");
                    result = result.TrimEnd('.', ',', ';', ':', '-');
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