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
    }
}