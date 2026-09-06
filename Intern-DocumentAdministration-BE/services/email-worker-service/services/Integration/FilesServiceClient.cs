using System.Net.Http.Headers;
using System.Text.Json;

namespace EmailWorkerService.Services.Integration;

public class FilesServiceClient : IFilesServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FilesServiceClient> _logger;

    public FilesServiceClient(
        HttpClient httpClient,
        ILogger<FilesServiceClient> logger,
        IConfiguration configuration,
        InternalServiceTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["Services:FilesService"]
                   ?? configuration["ServiceUrls:FilesService"]
                   ?? "http://localhost:5004";

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenProvider.GetToken());
    }

    public async Task<string?> UploadFileAsync(string fileName, byte[] fileBytes)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/files/upload", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "FilesService upload failed. Status: {Status}. Response: {Response}",
                    response.StatusCode,
                    responseString);
                return null;
            }

            using var jsonDocument = JsonDocument.Parse(responseString);
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean() &&
                root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while uploading {FileName} to FilesService.", fileName);
            return null;
        }
    }
}
