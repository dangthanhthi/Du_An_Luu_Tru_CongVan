using System;
using System.Threading.Tasks;

namespace AiOcrService.Services
{
    public class ExtractedDocumentData
    {
        public string? ReferenceNumber { get; set; }
        public string? Subject { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string? DocumentDateString { get; set; }
        public string? Signer { get; set; }
        public string? DocumentType { get; set; }
    }

    public interface IDynamicFieldExtractor
    {
        Task<ExtractedDocumentData> ExtractFieldsAsync(string extractedText);
    }
}
