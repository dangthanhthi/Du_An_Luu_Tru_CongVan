using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public interface IFilesServiceClient
    {
        Task<string?> UploadFileAsync(string fileName, byte[] fileBytes);
    }
}