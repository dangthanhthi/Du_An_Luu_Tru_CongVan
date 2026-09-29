using System.Collections.Generic;
using AiOcrService.Models;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Trách nhiệm duy nhất: suy luận Cơ quan ban hành / đối tác (PartnerName) - trước hết từ
    /// Số ký hiệu đã trích ở bước trước, sau đó fallback dò trong vùng header. Áp dụng
    /// Fuzzy-Correct (Bước 3) lên kết quả SAU KHI đã trích field.
    ///
    /// BƯỚC 6: thêm tham số "dynamicRules" (optional, mặc định null) làm LỚP DỰ PHÒNG CUỐI CÙNG
    /// nếu logic tĩnh không tìm ra PartnerName. Optional nên không phá lời gọi cũ.
    /// </summary>
    public interface IIssuingAgencyExtractor
    {
        void Extract(string headerText, string fullText, ExtractedDocumentData result, IReadOnlyList<OcrPatternRule>? dynamicRules = null);
    }
}
