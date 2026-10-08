using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentService;

public sealed class HttpTmsConnector : ITmsConnector
{
    private readonly HttpClient _http;

    public HttpTmsConnector(HttpClient http)
    {
        _http = http;
    }

    public async Task<StaffTaskPage> ListAsync(IReadOnlySet<Guid> users, int page, int size, CancellationToken ct)
    {
        var userQuery = string.Join(",", users);
        var url = $"/api/tms/tasks?users={Uri.EscapeDataString(userQuery)}&page={page}&size={size}";
        
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"TMS API returned status code {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<StaffTaskPage>(cancellationToken: ct);
        return result ?? new StaffTaskPage(Array.Empty<StaffTask>(), 0, page, size);
    }

    public async Task<TmsCreateResult> CreateAsync(Guid correlation, Guid document, Guid actor, Guid assignee, string title, CancellationToken ct)
    {
        var payload = new
        {
            CorrelationId = correlation,
            DocumentId = document,
            ActorId = actor,
            AssigneeId = assignee,
            Title = title
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/tms/tasks", payload, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TmsTaskResponse>(cancellationToken: ct);
                return new TmsCreateResult("Linked", data?.TaskId);
            }
        }
        catch (HttpRequestException)
        {
            // Transient failure
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }

        // Return PendingConfiguration on failure to allow retry via outbox/reconciliation
        return new TmsCreateResult("PendingConfiguration");
    }

    public async Task<TmsCreateResult> ReconcileAsync(Guid correlation, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsync($"/api/tms/tasks/{correlation}/reconcile", null, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TmsTaskResponse>(cancellationToken: ct);
                return new TmsCreateResult(data?.State ?? "Linked", data?.TaskId);
            }
        }
        catch (HttpRequestException)
        {
            // Ignore
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }

        return new TmsCreateResult("PendingConfiguration");
    }

    private sealed record TmsTaskResponse(string? TaskId, string? State);
}
