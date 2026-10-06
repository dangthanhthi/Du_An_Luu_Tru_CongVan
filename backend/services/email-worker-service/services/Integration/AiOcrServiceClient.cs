using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EmailWorkerService.Services.Integration;

public class AiOcrServiceClient : IAiOcrServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiOcrServiceClient> _logger;

    public AiOcrServiceClient(
        HttpClient httpClient,
        ILogger<AiOcrServiceClient> logger,
        IConfiguration configuration,
        InternalServiceTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["Services:AiOcrService"]
                   ?? configuration["ServiceUrls:AiOcrService"]
                   ?? "http://localhost:5006";

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenProvider.GetToken());
    }

    public async Task<OcrAnalyzeResult?> AnalyzeDocumentAsync(Guid fileId, string? senderEmail = null, string? fileName = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai-ocr/analyze", new
            {
                fileId,
                senderEmail,
                fileName
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                _logger.LogWarning(
                    "AiOcrService analyze failed. Status: {Status}. Response: {Response}",
                    response.StatusCode,
                    error);

                string message = error;

                try
                {
                    using var errorDoc = JsonDocument.Parse(error);

                    if (errorDoc.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        message = messageElement.GetString() ?? error;
                    }
                }
                catch
                {
                }

                throw new InvalidOperationException(
                    $"AI OCR failed ({(int)response.StatusCode}): {message}");
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataElem))
            {
                return null;
            }

            string? extractedText = dataElem.TryGetProperty("extractedText", out var t) ? t.GetString() : null;
            string? extractedRef = dataElem.TryGetProperty("extractedReferenceNumber", out var r) ? r.GetString() : null;
            string? extractedSubject = dataElem.TryGetProperty("extractedSubject", out var s) ? s.GetString() : null;

            DateTime? extractedDate = null;
            if (dataElem.TryGetProperty("extractedDate", out var d) &&
                d.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(d.GetString(), out var parsedDate))
            {
                extractedDate = parsedDate;
            }

            string? extractedSigner = dataElem.TryGetProperty("extractedSigner", out var sn) ? sn.GetString() : null;
            string? extractedDocType = dataElem.TryGetProperty("extractedDocumentType", out var dt) ? dt.GetString() : null;
            string? extractedPartnerName = dataElem.TryGetProperty("extractedPartnerName", out var pn) ? pn.GetString() : null;
            string? extractedDateString = dataElem.TryGetProperty("extractedDateString", out var ds) ? ds.GetString() : null;

            Guid? matchedPartnerId = null;
            if (dataElem.TryGetProperty("matchedPartnerId", out var p) &&
                p.ValueKind == JsonValueKind.String &&
                Guid.TryParse(p.GetString(), out var parsedGuid))
            {
                matchedPartnerId = parsedGuid;
            }

            var confidence = dataElem.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetDouble()
                : 0.0;
            var matchMethod = dataElem.TryGetProperty("matchMethod", out var m) ? m.GetString() : null;

            return new OcrAnalyzeResult(
                extractedText,
                extractedRef,
                extractedSubject,
                extractedDate,
                extractedSigner,
                extractedDocType,
                matchedPartnerId,
                confidence,
                matchMethod,
                extractedPartnerName,
                extractedDateString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while calling AiOcrService analyze.");
            return null;
        }
    }
}
