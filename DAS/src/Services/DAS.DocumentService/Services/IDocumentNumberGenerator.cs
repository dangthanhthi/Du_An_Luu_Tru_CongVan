using System.Threading.Tasks;

namespace DAS.DocumentService.Services
{
    public interface IDocumentNumberGenerator
    {
        Task<string> GenerateDocumentNumberAsync(string docType, int? year = null);
    }
}
