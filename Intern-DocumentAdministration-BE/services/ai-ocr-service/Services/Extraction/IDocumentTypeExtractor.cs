using System.Collections.Generic;
using AiOcrService.Models;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Trách nhiệm duy nhất: suy luận Loại văn bản (Quyết Định, Thông Báo, Công Văn...) -
    /// ưu tiên đọc hậu tố trong Số ký hiệu đã trích (VD /QĐ-), sau đó fallback dò nhãn
    /// in hoa trong header, cuối cùng fallback theo dấu hiệu "V/v" (mặc định Công Văn).
    /// PHẢI chạy SAU ReferenceNumberExtractor và SubjectExtractor vì phụ thuộc kết quả 2 field đó.
    ///
    /// BƯỚC 6: thêm tham số "dynamicRules" (optional, mặc định null) làm fallback cuối cùng nếu
    /// mọi nhánh tĩnh phía trên vẫn để DocumentType rỗng (trường hợp hiếm - loại văn bản mới
    /// chưa có trong lookup table cứng).
    /// </summary>
    public interface IDocumentTypeExtractor
    {
        void Extract(string headerText, ExtractedDocumentData result, IReadOnlyList<OcrPatternRule>? dynamicRules = null);
    }
}
