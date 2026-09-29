namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Trách nhiệm duy nhất: bóc tách Ngày ban hành, theo thứ tự ưu tiên 4 lớp: (1) chữ ký số
    /// điện tử trong nội dung, (2) dòng ngày ở góc phải header, (3) chữ ký số/siêu dữ liệu PDF,
    /// (4) tìm trong vùng trước "Căn cứ". Cần IGlyphNormalizer để đọc ngày/tháng bị biến dạng
    /// glyph do viết tay/OCR.
    /// </summary>
    public interface IDocumentDateExtractor
    {
        void Extract(string headerText, string fullText, string? fileName, int canCuIdx, byte[]? pdfBytes, ExtractedDocumentData result);
    }
}
