using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public class DocumentServiceClient : IDocumentServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DocumentServiceClient> _logger;

        public DocumentServiceClient(HttpClient httpClient, ILogger<DocumentServiceClient> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            var baseUrl = configuration["ServiceUrls:DocumentService"] ?? "http://localhost:5002";
            var token = configuration["InternalAuth:ServiceToken"] ?? string.Empty;

            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string?> RegisterIncomingDocumentAsync(string title, string fileId, DateTime receivedAt)
        {
            var requestBody = new
            {
                title = title,
                partnerId = (string?)null,
                fileId = fileId,
                receivedAt = receivedAt
            };

            var response = await _httpClient.PostAsJsonAsync("/api/documents/incoming", requestBody);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Lỗi tạo công văn. Status: {response.StatusCode}. Response: {responseString}");
                return null;
            }

            using var jsonDocument = JsonDocument.Parse(responseString);
            var root = jsonDocument.RootElement;

            if (root.GetProperty("success").GetBoolean())
            {
                var docData = root.GetProperty("data");
                return docData.GetProperty("id").GetString();
            }

            return null;
        }
    }
}