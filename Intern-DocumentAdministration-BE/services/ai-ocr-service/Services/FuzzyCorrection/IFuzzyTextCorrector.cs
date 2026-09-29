using System.Collections.Generic;

namespace AiOcrService.Services.FuzzyCorrection
{
    public record CorrectionSuggestion(string Original, string Corrected, int EditDistance);

    public interface IFuzzyTextCorrector
    {
        /// <summary>
        /// Sửa toàn bộ đoạn văn bản, thay từng từ nghi ngờ sai bằng từ đúng gần nhất
        /// trong từ điển hành chính, nếu tìm được trong ngưỡng edit-distance cho phép.
        /// Từ được bảo vệ (ProtectedTokenGuard) hoặc không tìm được ứng viên đủ gần
        /// sẽ được GIỮ NGUYÊN - không đoán mò khi không chắc chắn.
        /// </summary>
        string CorrectText(string text);

        /// <summary>Trả về danh sách các lần sửa đã thực hiện, phục vụ logging/audit/debug.</summary>
        IReadOnlyList<CorrectionSuggestion> LastCorrections { get; }
    }
}
