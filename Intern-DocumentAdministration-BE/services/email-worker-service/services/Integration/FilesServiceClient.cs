using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public class FilesServiceClient : IFilesServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FilesServiceClient> _logger;

        public FilesServiceClient(HttpClient httpClient, ILogger<FilesServiceClient> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Dùng ?? để gán giá trị mặc định nếu không tìm thấy trong cấu hình
            var baseUrl = configuration["ServiceUrls:FilesService"] ?? "http://localhost:5004";
            var token = configuration["InternalAuth:ServiceToken"] ?? string.Empty;

            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string?> UploadFileAsync(string fileName, byte[] fileBytes)
        {
            using var content = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");

            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/files/upload", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Lỗi khi upload. Status: {response.StatusCode}. Response: {responseString}");
                return null;
            }

            using var jsonDocument = JsonDocument.Parse(responseString);
            var root = jsonDocument.RootElement;

            if (root.GetProperty("success").GetBoolean())
            {
                // Thêm dấu ? để an toàn khi parse JSON
                return root.GetProperty("data").GetProperty("id").GetString();
            }

            return null;
        }
    }
}