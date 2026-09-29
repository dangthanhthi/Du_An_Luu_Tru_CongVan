using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AiOcrService.Models;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// BƯỚC 6: Áp dụng rule regex ĐỘNG (nạp từ DB qua IOcrRuleService, quản lý qua UI admin có sẵn
    /// - xem OcrPatternRule.cs / IOcrRuleService.cs) làm LỚP DỰ PHÒNG CUỐI CÙNG cho 1 field,
    /// CHỈ khi 5 Extractor tĩnh (Bước 5, hardcode regex) đã chạy hết mà field đó vẫn rỗng.
    ///
    /// Nguyên tắc bất di bất dịch (để không phá kết quả 100% held-out hiện có):
    /// 1. Rule động KHÔNG BAO GIỜ được phép GHI ĐÈ giá trị mà extractor tĩnh đã tìm ra - chỉ
    ///    được ĐIỀN VÀO CHỖ TRỐNG. Nếu cho ghi đè, một rule DB thêm sau này có thể âm thầm làm
    ///    hỏng field đang chạy đúng mà không ai review được (rule nằm trong DB, không nằm trong
    ///    Pull Request, không ai "diff" nó trước khi merge).
    /// 2. Một Pattern lỗi cú pháp regex (admin gõ sai qua UI TestPattern) KHÔNG được phép làm
    ///    crash toàn bộ pipeline trích xuất của CÁC FIELD KHÁC trong cùng 1 request - luôn bọc
    ///    try/catch quanh Regex.Match, coi rule lỗi là "không khớp" chứ không phải "lỗi hệ thống".
    /// 3. Vẫn phải chạy qua bộ Held-Out (Bước 1) trước/sau khi kích hoạt bất kỳ rule động nào,
    ///    dù về lý thuyết đây chỉ là lớp fallback an toàn - xem WIRING_NOTES_BUOC6.md mục 4.
    /// </summary>
    public static class DynamicRuleFallbackApplier
    {
        /// <summary>
        /// Thử từng rule đang active, đúng ruleType, sắp theo Priority GIẢM DẦN (rule ưu tiên cao
        /// hơn được thử trước) trên fullText. Trả về giá trị nhóm bắt đầu tiên (group 1) nếu pattern
        /// có nhóm, ngược lại trả về toàn bộ chuỗi khớp được. Trả về null nếu không rule nào khớp
        /// hoặc danh sách rules rỗng/null - KHÔNG đoán mò, giữ đúng nguyên tắc "để trống nếu không
        /// chắc chắn" xuyên suốt hệ thống.
        /// </summary>
        public static string? TryExtract(string fullText, string ruleType, IReadOnlyList<OcrPatternRule>? rules)
        {
            if (string.IsNullOrWhiteSpace(fullText) || rules == null || rules.Count == 0) return null;

            var candidateRules = rules
                .Where(r => r.IsActive && string.Equals(r.RuleType, ruleType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Priority);

            foreach (var rule in candidateRules)
            {
                if (string.IsNullOrWhiteSpace(rule.Pattern)) continue;

                Match m;
                try
                {
                    m = Regex.Match(fullText, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
                }
                catch (ArgumentException)
                {
                    // Pattern nhập qua UI admin có thể sai cú pháp regex - bỏ qua rule này,
                    // không để 1 rule lỗi làm hỏng việc trích field của các rule/field khác.
                    continue;
                }
                catch (RegexMatchTimeoutException)
                {
                    // Pattern quá tốn kém (catastrophic backtracking) trên văn bản dài - bỏ qua,
                    // không để 1 rule chậm làm treo cả request OCR.
                    continue;
                }

                if (!m.Success) continue;

                var val = (m.Groups.Count > 1 && m.Groups[1].Success) ? m.Groups[1].Value : m.Value;
                val = val?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }

            return null;
        }
    }
}
