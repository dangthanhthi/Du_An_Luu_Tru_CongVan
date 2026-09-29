using System.Collections.Generic;
using AiOcrService.Models;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Trách nhiệm duy nhất: bóc tách Số ký hiệu văn bản (VD: 1234/QĐ-UBND) từ vùng header,
    /// có đối chiếu chéo với tên tệp tin, và (khi thiếu số) đọc thêm từ Widget chữ ký số
    /// nhúng trong PDF gốc. Tách khỏi DynamicFieldExtractor (God Class) - Bước 5.
    ///
    /// BƯỚC 6: thêm tham số "dynamicRules" (optional, mặc định null) - danh sách OcrPatternRule
    /// active nạp từ DB, dùng làm LỚP DỰ PHÒNG CUỐI CÙNG nếu toàn bộ logic tĩnh phía trên không
    /// tìm ra ReferenceNumber. Tham số optional nên KHÔNG phá vỡ bất kỳ lời gọi cũ nào đang không
    /// truyền tham số này (tương thích ngược 100%).
    /// </summary>
    public interface IReferenceNumberExtractor
    {
        void Extract(
            string headerText,
            string fullText,
            string? fileName,
            byte[]? pdfBytes,
            ExtractedDocumentData result,
            IReadOnlyList<OcrPatternRule>? dynamicRules = null);
    }
}
