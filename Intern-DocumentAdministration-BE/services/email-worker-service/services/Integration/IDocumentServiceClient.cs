using System;
using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public interface IDocumentServiceClient
    {
        Task<string?> RegisterIncomingDocumentAsync(string title, string fileId, DateTime receivedAt);
    }
}