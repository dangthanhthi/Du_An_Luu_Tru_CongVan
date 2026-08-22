using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EmailWorkerService.Services.Integration
{
    public class AiOcrServiceClient : IAiOcrServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiOcrServiceClient> _logger;

        public AiOcrServiceClient(HttpClient httpClient, ILogger<AiOcrServiceClient> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            var baseUrl = configuration["ServiceUrls:AiOcrService"] ?? configuration["Services:AiOcrService"] ?? "http://localhost:5006";
            var token = configuration["InternalAuth:ServiceToken"] ?? string.Empty;

            _httpClient.BaseAddress = new Uri(baseUrl);
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<OcrAnalyzeResult?> AnalyzeDocumentAsync(Guid fileId, string? senderEmail = null)
        {
            try
            {
                var requestBody = new
                {
                    fileId = fileId,
                    senderEmail = senderEmail
                };

                var response = await _httpClient.PostAsJsonAsync("/api/ai-ocr/analyze", requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"[ai-ocr-service] Phân tích thất bại (Status: {response.StatusCode})");
                    return null;
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var dataElem))
                {
                    string? extractedText = dataElem.TryGetProperty("extractedText", out var t) ? t.GetString() : null;
                    string? extractedRef = dataElem.TryGetProperty("extractedReferenceNumber", out var r) ? r.GetString() : null;
                    string? extractedSubject = dataElem.TryGetProperty("extractedSubject", out var s) ? s.GetString() : null;
                    DateTime? extractedDate = null;
                    if (dataElem.TryGetProperty("extractedDate", out var d) && d.ValueKind == JsonValueKind.String && DateTime.TryParse(d.GetString(), out var parsedDate))
                    {
                        extractedDate = parsedDate;
                    }
                    string? extractedSigner = dataElem.TryGetProperty("extractedSigner", out var sn) ? sn.GetString() : null;
                    string? extractedDocType = dataElem.TryGetProperty("extractedDocumentType", out var dt) ? dt.GetString() : null;

                    Guid? matchedPartnerId = null;
                    if (dataElem.TryGetProperty("matchedPartnerId", out var p) && p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out var parsedGuid))
                    {
                        matchedPartnerId = parsedGuid;
                    }
                    double confidence = dataElem.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0;
                    string? matchMethod = dataElem.TryGetProperty("matchMethod", out var m) ? m.GetString() : null;

                    return new OcrAnalyzeResult(
                        extractedText, 
                        extractedRef, 
                        extractedSubject, 
                        extractedDate, 
                        extractedSigner, 
                        extractedDocType, 
                        matchedPartnerId, 
                        confidence, 
                        matchMethod);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ai-ocr-service] Gặp lỗi khi gọi AI OCR analyze");
                return null;
            }
        }
    }
}
