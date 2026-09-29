namespace AiOcrService.Services.Glyphs
{
    /// <summary>
    /// Trách nhiệm duy nhất: nhận một token thô (nghi ngờ là số bị OCR đọc sai glyph)
    /// và chuẩn hóa về chuỗi số thuần. KHÔNG được biết gì về PDF, regex trích xuất field,
    /// hay ngữ cảnh văn bản hành chính - chỉ làm việc trên 1 chuỗi ký tự đơn lẻ.
    /// Tách khỏi DynamicFieldExtractor để: (1) test được độc lập từng rule,
    /// (2) không ai lỡ gọi nhầm nó trên nguyên đoạn văn bản dài.
    /// </summary>
    public interface IGlyphNormalizer
    {
        /// <summary>
        /// True nếu token có khả năng là số bị biến dạng glyph (an toàn để normalize).
        /// False nếu token chứa chữ cái tiếng Việt hoặc ký tự lạ -> KHÔNG được đưa vào Normalize().
        /// Đây là "cửa khẩu" duy nhất bảo vệ Normalize() khỏi phá hủy văn bản thường.
        /// </summary>
        bool IsNumericToken(string raw);

        /// <summary>
        /// Chuẩn hóa token đã qua kiểm tra IsNumericToken == true thành chuỗi số thuần.
        /// Hành vi không xác định (không đảm bảo an toàn) nếu gọi khi IsNumericToken == false -
        /// caller PHẢI kiểm tra trước, tool không tự kiểm tra lại để tránh chi phí kép.
        /// </summary>
        string Normalize(string raw);

        /// <summary>
        /// Tiện ích gộp 2 bước trên: trả về chuỗi số nếu an toàn để normalize,
        /// ngược lại trả về token đã lọc chỉ giữ chữ số sẵn có (fallback không đoán mò).
        /// Đây là hàm caller (DynamicFieldExtractor) nên gọi trong 99% trường hợp thay vì
        /// tự viết lại logic if/else IsNumericToken ở từng nơi như hiện tại.
        /// </summary>
        string NormalizeOrFallback(string raw);
    }
}
