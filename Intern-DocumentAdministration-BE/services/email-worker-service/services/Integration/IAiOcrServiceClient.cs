using System;
using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public record OcrAnalyzeResult(
        string? ExtractedText,
        string? ExtractedReferenceNumber,
        string? ExtractedSubject,
        DateTime? ExtractedDate,
        string? ExtractedSigner,
        string? ExtractedDocumentType,
        Guid? MatchedPartnerId,
        double Confidence,
        string? MatchMethod
    );

    public interface IAiOcrServiceClient
    {
        Task<OcrAnalyzeResult?> AnalyzeDocumentAsync(Guid fileId, string? senderEmail = null);
    }
}
