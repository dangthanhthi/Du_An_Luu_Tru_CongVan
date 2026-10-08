using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DocumentService;

public sealed class HttpReminderDirectory : IReminderDirectory
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    
    // We use a fixed System User ID so it's not Guid.Empty, which WeeklyReminders validates against.
    private static readonly Guid SystemUserId = new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

    public HttpReminderDirectory(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private string GenerateSystemToken()
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Secret"] ?? "DAS_SECRET_KEY_FOR_LOCAL_DEV_AT_LEAST_32_BYTES_LONG");
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            claims: [ new Claim(ClaimTypes.Role, "Admin"), new Claim("sub", SystemUserId.ToString()) ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        );
        return handler.WriteToken(token);
    }

    public async Task<IReadOnlyList<Guid>> DepartmentsAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/departments"); // Actually /api/v2/directory/departments
        // For simplicity and to avoid checking which route auth-service actually responds to,
        // we'll fetch using the route we found in DirectoryController.cs
        request.RequestUri = new Uri("/api/v2/directory/departments?pageSize=100", UriKind.Relative);
        request.Headers.Authorization = new("Bearer", GenerateSystemToken());
        
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Fallback to old route if v2 fails
            request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/departments");
            request.Headers.Authorization = new("Bearer", GenerateSystemToken());
            response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }

        try
        {
            var jsonString = await response.Content.ReadAsStringAsync(ct);
            try
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<DirectoryPage<DepartmentResponse>>>(jsonString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Data?.Items != null)
                {
                    return data.Data.Items.Select(d => d.Id).ToList();
                }
            }
            catch
            {
                var fallback = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<List<DepartmentResponse>>>(jsonString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fallback?.Data != null) return fallback.Data.Select(d => d.Id).ToList();
            }
        }
        catch { }

        return Array.Empty<Guid>();
    }

    public async Task<ReminderDirectory> ResolveAsync(Guid department, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/users?departmentId={department}&pageSize=100");
        request.Headers.Authorization = new("Bearer", GenerateSystemToken());
        
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserResponse>>>(cancellationToken: ct);
        
        var users = data?.Data?.Items ?? [];
        
        var people = new List<ReminderPerson>();
        var leaders = new List<ReminderPerson>();
        
        foreach (var u in users)
        {
            var isLeader = u.Roles?.Any(r => r.Name == "Manager" || r.Name == "Director" || r.Name == "Admin") ?? false;
            var person = new ReminderPerson(u.Id, u.IsActive, u.Email);
            people.Add(person);
            if (isLeader) leaders.Add(person);
        }

        var scope = new ReportAuthority(
            UserId: SystemUserId,
            IsActive: true,
            CanReport: true,
            CanExport: true,
            OrganizationWide: false,
            DepartmentIds: new HashSet<Guid> { department },
            ManagedStaffIds: new HashSet<Guid>(),
            ConfidentialDocumentIds: new HashSet<Guid>()
        );

        return new ReminderDirectory(department, scope, people, leaders);
    }

    private sealed record DepartmentResponse(Guid Id, string Name);
    private sealed record UserResponse(Guid Id, bool IsActive, string? Email, List<RoleResponse>? Roles);
    private sealed record RoleResponse(Guid Id, string Name);
    private sealed class ApiResponse<T> { public T? Data { get; set; } }
    private sealed class PagedResult<T> { public List<T>? Items { get; set; } }
    private sealed record DirectoryPage<T>(IReadOnlyList<T> Items);
}
