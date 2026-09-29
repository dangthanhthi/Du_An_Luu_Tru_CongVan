using System.Collections.Generic;
using AiOcrService.Models;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Trách nhiệm duy nhất: bóc tách Trích yếu / Tiêu đề văn bản (4 tier fallback: V/v-pattern,
    /// tên loại văn bản in hoa, fallback V/v trong 1200 ký tự, "Điều 1: Ban hành..."). Áp dụng
    /// Fuzzy-Correct (Bước 3) lên kết quả SAU KHI đã trích field.
    ///
    /// BƯỚC 6: thêm tham số "dynamicRules" (optional, mặc định null) làm TIER 5 - chỉ chạy khi
    /// cả 4 tier tĩnh phía trên đều không tìm ra Subject.
    /// </summary>
    public interface ISubjectExtractor
    {
        void Extract(string headerText, string fullText, ExtractedDocumentData result, IReadOnlyList<OcrPatternRule>? dynamicRules = null);
    }
}
