using System;
using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public interface IDocumentServiceClient
    {
        Task<string?> RegisterIncomingDocumentAsync(
            string title,
            string? referenceNumber,
            Guid? partnerId,
            Guid fileId,
            DateTime receivedAt);

        Task<string?> RegisterDocumentAsync(
            string documentType,
            string title,
            string? referenceNumber,
            Guid? partnerId,
            Guid fileId,
            DateTime receivedAt,
            Guid? departmentId = null,
            string? summary = null);

        Task<HashSet<string>> GetExistingDocumentIdsAsync(CancellationToken cancellationToken = default);
    }
}