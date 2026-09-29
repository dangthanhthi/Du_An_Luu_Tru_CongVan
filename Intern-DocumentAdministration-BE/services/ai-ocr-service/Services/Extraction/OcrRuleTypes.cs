namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Hằng số tên RuleType dùng để lọc OcrPatternRule theo từng field - tránh gõ tay chuỗi
    /// "ReferenceNumber" rải rác nhiều nơi rồi lệch chính tả (VD "Reference_Number" ở 1 chỗ,
    /// "ReferenceNumber" ở chỗ khác) khiến rule DB không bao giờ khớp mà không ai biết vì sao.
    /// Đây cũng chính là danh sách giá trị HỢP LỆ cho cột RuleType khi admin thêm rule mới
    /// qua UI (CreateOcrRuleRequest.RuleType) - nên hiển thị y hệt các hằng số này làm dropdown
    /// thay vì để admin tự gõ tay chuỗi tự do.
    /// </summary>
    public static class OcrRuleTypes
    {
        public const string ReferenceNumber = "ReferenceNumber";
        public const string IssuingAgency = "IssuingAgency";
        public const string Subject = "Subject";
        public const string DocumentType = "DocumentType";
    }
}
