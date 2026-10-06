using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace DocumentService;

public class PartnerServiceClient : IPartnerServiceClient
{
    private readonly HttpClient _httpClient;

    public PartnerServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"/api/partners/{partnerId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) throw new ExternalEntityDependencyException();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("isActive", out var active) || active.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !data.TryGetProperty("isDeleted", out var deleted) || deleted.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                (active.GetBoolean() && deleted.GetBoolean())) throw new ExternalEntityDependencyException();
            var partner = data.Deserialize<PartnerDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (partner is null || partner.Id != partnerId || string.IsNullOrWhiteSpace(partner.FullName) || partner.FullName.Length > 500 ||
                partner.EntityType is not ("Sender" or "Recipient" or "Both")) throw new ExternalEntityDependencyException();
            return partner;
        }
        catch (HttpRequestException) { throw new ExternalEntityDependencyException(); }
        catch (OperationCanceledException) { throw new ExternalEntityDependencyException(); }
        catch (JsonException) { throw new ExternalEntityDependencyException(); }
    }
}

public sealed class ExternalEntityDependencyException : HttpRequestException
{
    public ExternalEntityDependencyException() : base("External Entity service is unavailable.", null, System.Net.HttpStatusCode.ServiceUnavailable) { }
}

public class FilesServiceClient : IFilesServiceClient
{
    private readonly HttpClient _httpClient;

    public FilesServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"/api/files/{fileId}/info");
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<FileMetadataDto>>();
            var file = result?.Data;
            if (result?.Success != true || file is null || file.Id != fileId ||
                file.State != "Available" || !file.CanDownload || !file.CanAttach ||
                file.ContentType != "application/pdf" || file.SizeBytes <= 0 ||
                file.SizeBytes > 25L * 1024 * 1024 ||
                file.Sha256 is not { Length: 64 } ||
                !file.Sha256.All(Uri.IsHexDigit))
                return null;

            return file;
        }
        catch
        {
            return null; // Graceful fallback if files-service is offline
        }
    }
}

public class NotificationServiceClient : INotificationServiceClient
{
    private readonly HttpClient _httpClient;

    public NotificationServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> SendNotificationAsync(SendNotificationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/notifications/send", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false; // Graceful fallback if notification-service is offline
        }
    }
}

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
