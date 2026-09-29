using System.Text.RegularExpressions;

namespace AiOcrService.Services.Glyphs
{
    /// <summary>
    /// Một quy tắc chuẩn hóa glyph đơn lẻ. Biến chuỗi 20+ dòng Regex.Replace nối tiếp
    /// (khó đọc, khó test riêng lẻ) thành 1 danh sách có thể duyệt, log, và unit-test
    /// từng phần tử độc lập mà không phải mock cả hàm lớn.
    /// </summary>
    public sealed record GlyphRule(string Description, Regex Pattern, string Replacement)
    {
        public static GlyphRule Create(string description, string pattern, string replacement,
            RegexOptions options = RegexOptions.None)
            => new(description, new Regex(pattern, options | RegexOptions.Compiled), replacement);

        public string Apply(string input) => Pattern.Replace(input, Replacement);
    }
}
