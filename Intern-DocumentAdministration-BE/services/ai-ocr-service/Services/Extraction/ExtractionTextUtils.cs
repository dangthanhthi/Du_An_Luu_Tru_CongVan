using System.Globalization;
using System.Text;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Các hàm tiện ích dùng CHUNG giữa nhiều Extractor (Bước 5). Trước Bước 5, mỗi hàm này
    /// là 1 private static nằm rải rác trong DynamicFieldExtractor (God Class) - gộp lại đây
    /// để chỉ có DUY NHẤT một bản triển khai, tránh nguy cơ 2-3 Extractor tự copy rồi
    /// sửa lệch nhau theo thời gian. Hành vi giữ NGUYÊN VẸN so với bản gốc.
    /// </summary>
    public static class ExtractionTextUtils
    {
        /// <summary>Gốc: DynamicFieldExtractor.RemoveDiacritics (dùng trong ReferenceNumber, IssuingAgency, Subject, DocumentType).</summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC)
                .Replace("đ", "d").Replace("Đ", "D");
        }

        /// <summary>Gốc: DynamicFieldExtractor.IsValidDate (dùng trong DocumentDateExtractor).</summary>
        public static bool IsValidDate(int d, int m, int y)
        {
            return d >= 1 && d <= 31 && m >= 1 && m <= 12 && y >= 1990 && y <= 2050;
        }

        /// <summary>Gốc: DynamicFieldExtractor.CapitalizeFirst (dùng trong SubjectExtractor).</summary>
        public static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        /// <summary>Gốc: DynamicFieldExtractor.IsValidSubject (dùng trong SubjectExtractor).</summary>
        public static bool IsValidSubject(string? val)
        {
            if (string.IsNullOrWhiteSpace(val) || val.Length < 5) return false;
            var lower = val.ToLowerInvariant();
            if (lower.Contains("file:///") || lower.Contains("http://") || lower.Contains("https://")) return false;
            if (lower.Contains("email:") || lower.Contains("điện thoại:") || lower.Contains("mã số thuế:")) return false;
            if (lower.Contains(".gov.vn") || lower.Contains("@")) return false;
            return true;
        }

        /// <summary>
        /// Tách vùng Header thành 2 nửa: Left (Top-Left: Cơ quan, Số hiệu) và Right (Top-Right: Quốc hiệu, Địa danh ngày tháng).
        /// </summary>
        public static (string Left, string Right) SplitHeaderLeftRight(string headerText)
        {
            if (string.IsNullOrWhiteSpace(headerText)) return (string.Empty, string.Empty);

            var lines = headerText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            var leftLines = new System.Collections.Generic.List<string>();
            var rightLines = new System.Collections.Generic.List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                int congHoaIdx = line.IndexOf("CỘNG HÒA", System.StringComparison.OrdinalIgnoreCase);
                if (congHoaIdx < 0) congHoaIdx = line.IndexOf("CONG HOA", System.StringComparison.OrdinalIgnoreCase);

                int docLapIdx = line.IndexOf("Độc lập", System.StringComparison.OrdinalIgnoreCase);
                if (docLapIdx < 0) docLapIdx = line.IndexOf("Doc lap", System.StringComparison.OrdinalIgnoreCase);

                var haNoiMatch = System.Text.RegularExpressions.Regex.Match(line, @"\b(?:Hà Nội|TP\.|TP\s+Hồ Chí Minh|Đà Nẵng|Bắc Ninh|Hải Phòng|Cần Thơ|ngày\s+\d)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                int haNoiIdx = haNoiMatch.Success ? haNoiMatch.Index : -1;

                int splitIdx = -1;
                if (congHoaIdx > 10) splitIdx = congHoaIdx;
                else if (docLapIdx > 10) splitIdx = docLapIdx;
                else if (haNoiIdx > 10) splitIdx = haNoiIdx;

                if (splitIdx > 0)
                {
                    leftLines.Add(line.Substring(0, splitIdx).Trim());
                    rightLines.Add(line.Substring(splitIdx).Trim());
                }
                else
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"(?:CỘNG\s*H[OÒ]A|Độc\s*lập|Hà Nội|ngày\s+\d)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        rightLines.Add(line);
                    }
                    else
                    {
                        leftLines.Add(line);
                    }
                }
            }

            return (string.Join("\n", leftLines), string.Join("\n", rightLines));
        }
    }
}
