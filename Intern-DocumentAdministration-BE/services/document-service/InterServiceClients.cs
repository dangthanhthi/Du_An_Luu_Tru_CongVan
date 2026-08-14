using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace DocumentService;

/// <summary>
/// DTO nhận từ partner-service. Field names khớp chính xác với JSON response của partner-service
/// (FullName / ShortName theo schema.sql của partner schema).
/// </summary>
public class PartnerDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = default!;

    [JsonPropertyName("shortName")]
    public string ShortName { get; init; } = default!;

    [JsonPropertyName("entityType")]
    public string EntityType { get; init; } = default!; // Sender / Recipient / Both

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }
}

public class FileMetadataDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("originalName")]
    public string FileName { get; init; } = default!;

    [JsonPropertyName("storagePath")]
    public string FilePath { get; init; } = default!;

    [JsonPropertyName("sizeBytes")]
    public long FileSize { get; init; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = default!;
}

public record SendNotificationRequest(
    string RecipientEmail,
    string Subject,
    string Body
);

public interface IPartnerServiceClient
{
    Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId);
}

public interface IFilesServiceClient
{
    Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId);
}

public interface INotificationServiceClient
{
    Task<bool> SendNotificationAsync(SendNotificationRequest request);
}

/// <summary>
/// DTO nhận từ auth-service khi lookup user theo role.
/// </summary>
public class UserInfoDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = default!;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = default!;
}

public interface IAuthServiceClient
{
    Task<UserInfoDto?> GetUserByRoleAsync(string role);
}

public class PartnerServiceClient : IPartnerServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PartnerServiceClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"/api/partners/{partnerId}");
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                req.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }

            var response = await _httpClient.SendAsync(req);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Không có quyền truy cập từ partner-service.");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw new Exception($"[503_SERVICE_UNAVAILABLE] [partner-service] Phản hồi lỗi: {(int)response.StatusCode} {response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PartnerDto>>();
            return result?.Data;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"[503_SERVICE_UNAVAILABLE] [partner-service] {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            throw new Exception($"[503_SERVICE_UNAVAILABLE] [partner-service] Connection timeout: {ex.Message}");
        }
    }
}

public class FilesServiceClient : IFilesServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FilesServiceClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"/api/files/{fileId}/info");
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                req.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }

            var response = await _httpClient.SendAsync(req);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Không có quyền truy cập từ files-service.");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw new Exception($"[503_SERVICE_UNAVAILABLE] [files-service] Phản hồi lỗi: {(int)response.StatusCode} {response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<FileMetadataDto>>();
            return result?.Data;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"[503_SERVICE_UNAVAILABLE] [files-service] {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            throw new Exception($"[503_SERVICE_UNAVAILABLE] [files-service] Connection timeout: {ex.Message}");
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
            // Bắt ngoại lệ để không crash nghiệp vụ chuyển trạng thái công văn khi notification-service chưa chạy ở local
            return false;
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

public class AuthServiceClient : IAuthServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthServiceClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UserInfoDto?> GetUserByRoleAsync(string role)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"/api/users?role={Uri.EscapeDataString(role)}");
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                req.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }

            var response = await _httpClient.SendAsync(req);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Không có quyền tra cứu user từ auth-service.");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw new Exception($"[503_SERVICE_UNAVAILABLE] [auth-service] Phản hồi lỗi khi tìm user với role '{role}': {(int)response.StatusCode} {response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserInfoDto>>();
            return result?.Data;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"[503_SERVICE_UNAVAILABLE] [auth-service] {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            throw new Exception($"[503_SERVICE_UNAVAILABLE] [auth-service] Connection timeout: {ex.Message}");
        }
    }
}

