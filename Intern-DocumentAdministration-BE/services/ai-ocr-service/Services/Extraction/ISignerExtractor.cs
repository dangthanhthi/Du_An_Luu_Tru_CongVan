namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Trách nhiệm duy nhất: dò 20 dòng cuối văn bản để tìm tên Người ký - dòng 2-5 từ,
    /// mỗi từ viết hoa chữ cái đầu, không chứa số, và không phải cụm chức danh (Chủ tịch,
    /// Giám đốc...). Không phụ thuộc service nào khác.
    /// </summary>
    public interface ISignerExtractor
    {
        void Extract(string fullText, ExtractedDocumentData result);
    }
}
