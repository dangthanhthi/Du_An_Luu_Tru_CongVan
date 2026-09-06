using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

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
            var response = await _httpClient.GetAsync($"/api/partners/{partnerId}");
            if (!response.IsSuccessStatusCode) return null;
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PartnerDto>>();
            return result?.Data;
        }
        catch
        {
            return null; // Graceful fallback if partner-service is offline
        }
    }
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
            var response = await _httpClient.GetAsync($"/api/files/{fileId}");
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<FileMetadataDto>>();
            return result?.Data;
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
