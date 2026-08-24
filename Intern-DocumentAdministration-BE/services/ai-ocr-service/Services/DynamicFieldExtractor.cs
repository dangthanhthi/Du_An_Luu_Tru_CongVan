using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AiOcrService.Services
{
    public class DynamicFieldExtractor : IDynamicFieldExtractor
    {
        private readonly IOcrRuleService _ruleService;

        public DynamicFieldExtractor(IOcrRuleService ruleService)
        {
            _ruleService = ruleService;
        }

        public async Task<ExtractedDocumentData> ExtractFieldsAsync(string extractedText)
        {
            var result = new ExtractedDocumentData();
            if (string.IsNullOrWhiteSpace(extractedText)) return result;

            var activeRules = await _ruleService.GetRulesAsync(isActive: true);

            // 1. BÓC TÁCH SỐ KÝ HIỆU (ReferenceNumber)
            var refRules = activeRules
                .Where(r => r.RuleType.Equals("ReferenceNumber", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            var refCandidates = new System.Collections.Generic.List<(string Value, int Priority)>();
            foreach (var rule in refRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, 
                        RegexOptions.IgnoreCase | RegexOptions.Multiline,
                        TimeSpan.FromSeconds(2));
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = Regex.Replace(val, @"\s*[\/\\|]\s*", "/");
                        val = val.TrimEnd('.', ',', ';', ':', '-', ' ');
                        if (val.Length >= 2 && !val.Equals("ngay", StringComparison.OrdinalIgnoreCase))
                        {
                            refCandidates.Add((val, rule.Priority));
                        }
                    }
                }
                catch { }
            }
            if (refCandidates.Count > 0)
            {
                // Pick highest priority (lowest number), then longest value
                result.ReferenceNumber = refCandidates
                    .OrderBy(c => c.Priority)
                    .ThenByDescending(c => c.Value.Length)
                    .First().Value;
            }

            // 2. BÓC TÁCH TRÍCH YẾU / TIÊU ĐỀ (Subject)
            var subjectRules = activeRules
                .Where(r => r.RuleType.Equals("Subject", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            var subjectCandidates = new System.Collections.Generic.List<(string Value, int Priority)>();
            foreach (var rule in subjectRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, 
                        RegexOptions.IgnoreCase | RegexOptions.Multiline,
                        TimeSpan.FromSeconds(2));
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = val.TrimEnd('.', ',', ';', ':', '-', ' ');
                        // Bỏ các ký tự xuống dòng nếu trích yếu trải dài
                        val = Regex.Replace(val, @"\s+", " ");
                        if (val.Length >= 5)
                        {
                            subjectCandidates.Add((val, rule.Priority));
                        }
                    }
                }
                catch { }
            }
            if (subjectCandidates.Count > 0)
            {
                result.Subject = subjectCandidates
                    .OrderBy(c => c.Priority)
                    .ThenByDescending(c => c.Value.Length)
                    .First().Value;
            }

            // 3. BÓC TÁCH NGÀY BAN HÀNH (DocumentDate)
            var dateRules = activeRules
                .Where(r => r.RuleType.Equals("DocumentDate", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            foreach (var rule in dateRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (match.Success)
                    {
                        if (match.Groups.Count >= 4)
                        {
                            // Dạng: ngày {1} tháng {2} năm {3}
                            if (int.TryParse(match.Groups[1].Value, out var day) &&
                                int.TryParse(match.Groups[2].Value, out var month) &&
                                int.TryParse(match.Groups[3].Value, out var year))
                            {
                                if (day >= 1 && day <= 31 && month >= 1 && month <= 12 && year >= 1900 && year <= 2100)
                                {
                                    result.DocumentDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                                    result.DocumentDateString = $"{day:D2}/{month:D2}/{year}";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var dateStr = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" };
                            if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                            {
                                result.DocumentDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                                result.DocumentDateString = parsedDate.ToString("dd/MM/yyyy");
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            // 4. BÓC TÁCH NGƯỜI KÝ & CHỨC DANH (Signer)
            var signerRules = activeRules
                .Where(r => r.RuleType.Equals("Signer", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            foreach (var rule in signerRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = Regex.Replace(val, @"\s+", " ");
                        if (val.Length >= 3)
                        {
                            result.Signer = val;
                            break;
                        }
                    }
                }
                catch { }
            }

            // 5. BÓC TÁCH & PHÂN LOẠI LOẠI VĂN BẢN (DocumentType)
            var typeRules = activeRules
                .Where(r => r.RuleType.Equals("DocumentType", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            foreach (var rule in typeRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        result.DocumentType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(val.ToLowerInvariant());
                        break;
                    }
                }
                catch { }
            }

            return result;
        }
    }
}
