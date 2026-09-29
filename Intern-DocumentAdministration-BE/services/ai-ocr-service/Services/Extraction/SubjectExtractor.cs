using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AiOcrService.Models;
using AiOcrService.Services.FuzzyCorrection;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Bóc tách Trích yếu / Tiêu đề (Subject). Toàn bộ logic Bước 5 (4 tier) giữ NGUYÊN VẸN.
    /// BƯỚC 6 thêm TIER 5 - rule động từ DB - chạy SAU CÙNG, chỉ khi cả 4 tier tĩnh đều để
    /// Subject rỗng, và đặt TRƯỚC lời gọi ApplyFuzzyCorrection() cuối hàm để value từ rule động
    /// cũng được hưởng lợi từ việc sửa lỗi chính tả tự động giống mọi tier khác.
    /// </summary>
    public sealed class SubjectExtractor : ISubjectExtractor
    {
        private readonly IFuzzyTextCorrector _fuzzyCorrector;

        public SubjectExtractor(IFuzzyTextCorrector fuzzyCorrector)
        {
            _fuzzyCorrector = fuzzyCorrector;
        }

        public void Extract(string headerText, string fullText, ExtractedDocumentData result, IReadOnlyList<OcrPatternRule>? dynamicRules = null)
        {
            if (string.IsNullOrWhiteSpace(fullText)) return;

            // 1. BÓC TÁCH TRÍCH YẾU / TIÊU ĐỀ (Subject) chuẩn theo Seleton-VN/Intern-DocumentAdministration-BE (main)
            var subjectRules = (dynamicRules ?? OcrRuleService.GetDefaultRules())
                .Where(r => r.RuleType.Equals("Subject", StringComparison.OrdinalIgnoreCase) && r.IsActive)
                .OrderBy(r => r.Priority);

            var subjectCandidates = new List<(string Value, int Priority)>();

            foreach (var rule in subjectRules)
            {
                try
                {
                    // Đảm bảo pattern kết thúc bằng |\Z thay vì |$ để không bị ngắt ở dòng đầu tiên khi chạy RegexOptions.Multiline
                    var pattern = Regex.Replace(rule.Pattern, @"\|\$\)", @"|\Z)");
                    var match = Regex.Match(fullText, pattern,
                        RegexOptions.IgnoreCase | RegexOptions.Multiline,
                        TimeSpan.FromSeconds(2));

                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = val.TrimEnd('.', ',', ';', ':', '-', ' ');
                        val = Regex.Replace(val, @"\s+", " ");

                        bool isVeViec = Regex.IsMatch(val, @"^(?:Về\s*việc|Ve\s*viec)", RegexOptions.IgnoreCase) ||
                                        Regex.IsMatch(match.Value, @"^(?:\s*|\n)*(?:Về\s*việc|Ve\s*viec)", RegexOptions.IgnoreCase);

                        // Loại bỏ prefix thừa theo chuẩn origin/main
                        val = Regex.Replace(val, @"^(?:Về việc|VỀ VIỆC|Ve viec|V[\/\\]v|V\.v|Trích yếu|TRÍCH YẾU|Regarding|Subject)\s*[:.:]?\s*", "", RegexOptions.IgnoreCase).Trim();

                        if (isVeViec && !val.StartsWith("Về việc", StringComparison.OrdinalIgnoreCase))
                        {
                            val = "Về việc " + val;
                        }

                        val = Regex.Replace(val, @"(?:\s+(?:ỦY|Y)\s*BAN\s*NHÂN\s*DÂN.*|\s+CH\s*TCH.*|\s+UBND.*)$", "", RegexOptions.IgnoreCase).Trim();
                        val = Regex.Replace(val, @"\s*\([^\)]*(?:Văn thư|Lưu tr|Chi cc|Chi cục|Sở Nội vụ)[^\)]*\)$", "", RegexOptions.IgnoreCase).Trim();

                        if (ExtractionTextUtils.IsValidSubject(val))
                        {
                            subjectCandidates.Add((val, rule.Priority));
                        }
                    }
                }
                catch { }
            }

            if (subjectCandidates.Count > 0)
            {
                result.Subject = ExtractionTextUtils.CapitalizeFirst(
                    subjectCandidates
                        .OrderBy(c => c.Priority)
                        .ThenByDescending(c => c.Value.Length)
                        .First().Value
                );
                return;
            }

            // Fallback: Tìm "Điều 1: Ban hành..." cho Quyết định không có V/v
            var dieu1Match = Regex.Match(fullText, @"(?:Điều|ĐIỀU|Điu|\?iu|iu)\s*1\s*[\.:]?\s*(Ban\s*hành\s*(?:kèm\s*theo[^\n]{0,100})?)\s*([\s\S]{10,250}?)(?=\.[\s\S]{0,5}iu\s*2|$)", RegexOptions.IgnoreCase);
            if (dieu1Match.Success)
            {
                var val = dieu1Match.Groups[2].Value.Trim();
                val = Regex.Replace(val, @"\s+", " ");
                var prefix = dieu1Match.Groups[1].Value.Trim().Replace('\n', ' ').Replace('\r', ' ');
                val = Regex.Replace(prefix, @"\s+", " ") + " " + val;
                val = Regex.Replace(val, @"\s*\([^\)]*(?:Văn thư|Lưu tr|Chi cc|Chi cục|Sở Nội vụ)[^\)]*\)$", "", RegexOptions.IgnoreCase).Trim();
                if (ExtractionTextUtils.IsValidSubject(val))
                {
                    result.Subject = ExtractionTextUtils.CapitalizeFirst(val);
                }
            }

            // Fallback: Tiêu đề đứng ngay dưới THÔNG TƯ / QUYẾT ĐỊNH / KẾ HOẠCH (không có V/v)
            if (string.IsNullOrWhiteSpace(result.Subject))
            {
                var headerTypeSubjectMatch = Regex.Match(fullText, @"(?:THÔNG\s*TƯ|QUYẾT\s*ĐỊNH|KẾ\s*HOẠCH|THÔNG\s*BÁO)\s+([\s\S]{10,250}?)(?=\s+(?:Thông\s*tư\s*số|Căn\s*cứ|Điều\s+1|\Z))", RegexOptions.IgnoreCase);
                if (headerTypeSubjectMatch.Success)
                {
                    var candSubject = headerTypeSubjectMatch.Groups[1].Value.Trim();
                    candSubject = Regex.Replace(candSubject, @"\s+", " ");
                    if (ExtractionTextUtils.IsValidSubject(candSubject))
                    {
                        result.Subject = ExtractionTextUtils.CapitalizeFirst(candSubject);
                    }
                }
            }
        }
    }
}
