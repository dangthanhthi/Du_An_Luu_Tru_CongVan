using System.Linq;
using System.Text.RegularExpressions;

namespace AiOcrService.Services.FuzzyCorrection
{
    /// <summary>
    /// Trước khi thay 1 từ bằng gợi ý fuzzy-match, PHẢI chắc chắn từ đó không phải là
    /// mã luật/số hiệu/viết tắt hợp lệ (QH13, NĐ30, COVID19, ISO15189, 561/SYT-VP...).
    /// Đây chính là vấn đề dự án đã từng gặp với "IsLowQualityText" - fuzzy match càng mạnh
    /// càng dễ tái phạm lỗi này nếu không có guard rõ ràng, vì các mã này thường CÓ vẻ giống
    /// một từ bị OCR sai (chữ + số dính nhau).
    /// </summary>
    public static class ProtectedTokenGuard
    {
        private static readonly Regex ContainsDigit = new(@"\d", RegexOptions.Compiled);
        private static readonly Regex AllCapsAbbreviation = new(@"^[A-ZĐ]{2,}[\-/]?[A-ZĐ0-9]*$", RegexOptions.Compiled);
        private static readonly Regex LooksLikeReferenceNumber = new(@"^\d+[/\-][A-ZĐ0-9\-]+$", RegexOptions.Compiled);

        public static bool IsProtected(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return true;

            // Mã luật/tiêu chuẩn dạng CHỮ+SỐ dính liền (QH13, NĐ30, ISO15189, COVID19...)
            if (ContainsDigit.IsMatch(token) && AllCapsAbbreviation.IsMatch(token.ToUpperInvariant()))
                return true;

            // Số hiệu văn bản dạng 561/SYT-VP, 11930/SYT-NVY...
            if (LooksLikeReferenceNumber.IsMatch(token))
                return true;

            // Toàn bộ là số (ngày/tháng/năm, số điện thoại...) - không có "gần đúng" nào có nghĩa
            if (ContainsDigit.IsMatch(token) && !token.Any(char.IsLetter))
                return true;

            // Từ ngữ hành chính / y tế chuẩn xác không bao giờ được phép bị fuzzy corrector làm biến dạng
            if (token.Equals("thuốc", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("lưới", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("kháng", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("mạng", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("gắn", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("sát", System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Từ quá ngắn (<=2 ký tự) - fuzzy match trên từ ngắn dễ sinh false positive nghiêm trọng
            // (VD "là", "và" có thể "gần" hàng chục từ khác trong khoảng cách edit=1)
            if (token.Length <= 2)
                return true;

            return false;
        }
    }
}
