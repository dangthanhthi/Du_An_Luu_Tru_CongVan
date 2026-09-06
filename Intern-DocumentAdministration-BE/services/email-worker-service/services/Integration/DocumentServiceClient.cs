using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EmailWorkerService.Services.Integration;

public class DocumentServiceClient : IDocumentServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocumentServiceClient> _logger;

    public DocumentServiceClient(
        HttpClient httpClient,
        ILogger<DocumentServiceClient> logger,
        IConfiguration configuration,
        InternalServiceTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["Services:DocumentService"]
                   ?? configuration["ServiceUrls:DocumentService"]
                   ?? "http://localhost:5002";

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenProvider.GetToken());
    }

    public Task<string?> RegisterIncomingDocumentAsync(
        string title,
        string? referenceNumber,
        Guid? partnerId,
        Guid fileId,
        DateTime receivedAt)
    {
        return RegisterDocumentAsync("incoming", title, referenceNumber, partnerId, fileId, receivedAt);
    }

    public async Task<string?> RegisterDocumentAsync(
        string documentType,
        string title,
        string? referenceNumber,
        Guid? partnerId,
        Guid fileId,
        DateTime receivedAt,
        Guid? departmentId = null,
        string? summary = null)
    {
        try
        {
            var normalizedType = (documentType ?? "incoming").Trim().ToLowerInvariant();
            string endpoint;
            object requestBody;

            var finalSummary = !string.IsNullOrWhiteSpace(summary)
                ? summary
                : string.IsNullOrWhiteSpace(referenceNumber)
                    ? null
                    : $"Số/Ký hiệu: {referenceNumber}";

            if (normalizedType.Contains("internal") || normalizedType.Contains("noibo") || normalizedType == "nb")
            {
                endpoint = "/api/documents/internal";
                requestBody = new
                {
                    title,
                    summary = finalSummary,
                    senderDepartmentId = departmentId,
                    attachmentFileIds = new[] { fileId }
                };
            }
            else if (normalizedType.Contains("outgoing") || normalizedType.Contains("di"))
            {
                endpoint = "/api/documents/outgoing";
                requestBody = new
                {
                    title,
                    summary = finalSummary,
                    partnerId = partnerId,
                    senderDepartmentId = departmentId,
                    attachmentFileIds = new[] { fileId }
                };
            }
            else
            {
                endpoint = "/api/documents/incoming";
                requestBody = new
                {
                    title,
                    summary = finalSummary,
                    partnerId,
                    receivedAt,
                    attachmentFileIds = new[] { fileId }
                };
            }

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "DocumentService create {DocType} failed. Status: {Status}. Response: {Response}",
                    normalizedType,
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
            _logger.LogError(ex, "Error while creating document of type {DocType}.", documentType);
            return null;
        }
    }

    public async Task<HashSet<string>> GetExistingDocumentIdsAsync(CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var response = await _httpClient.GetAsync("/api/documents", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var jsonDocument = JsonDocument.Parse(responseString);
                var root = jsonDocument.RootElement;

                JsonElement itemsElement = default;
                if (root.TryGetProperty("data", out var dataEl))
                {
                    if (dataEl.ValueKind == JsonValueKind.Array) itemsElement = dataEl;
                    else if (dataEl.TryGetProperty("items", out var itemsEl)) itemsElement = itemsEl;
                }

                if (itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idEl))
                        {
                            var idStr = idEl.GetString();
                            if (!string.IsNullOrWhiteSpace(idStr))
                            {
                                result.Add(idStr);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve existing documents from DocumentService.");
        }
        return result;
    }
}
