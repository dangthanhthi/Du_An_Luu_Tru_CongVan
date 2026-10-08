using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NotificationService.Tests;

public sealed class NotificationInboxFlowTests
{
    [Fact]
    public async Task Http_inbox_pages_unread_and_read_all_are_owned_by_the_current_recipient()
    {
        await using var host = new NotificationHttpTests.Host();
        var recipient = Guid.NewGuid(); var otherRecipient = Guid.NewGuid();
        using var sender = host.Client(Guid.NewGuid(), "NotificationSend");
        for (var i = 0; i < 3; i++)
        {
            sender.DefaultRequestHeaders.Remove("Idempotency-Key"); sender.DefaultRequestHeaders.Add("Idempotency-Key", "fixture-" + i);
            Assert.Equal(HttpStatusCode.Accepted, (await sender.PostAsJsonAsync("/api/notifications/send", new
            { recipientUserId = i < 2 ? recipient : otherRecipient, subject = "Fixture " + i, body = "Synthetic inbox", actionUrl = "/vi/apps/documents/list" })).StatusCode);
        }
        using var owner = host.Client(recipient); using var other = host.Client(otherRecipient);
        var first = await Data(await owner.GetAsync("/api/notifications/my?page=1&pageSize=1"));
        var second = await Data(await owner.GetAsync("/api/notifications/my?page=2&pageSize=1"));
        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.EndsWith("Z", first.GetProperty("items")[0].GetProperty("createdAt").GetString());
        var id = first.GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.NotEqual(id, second.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(2, (await Data(await owner.GetAsync("/api/notifications/unread-count"))).GetProperty("unreadCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"/api/notifications/{id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsync($"/api/notifications/{id}/read", null)).StatusCode);
        var readPage = await Data(await owner.GetAsync("/api/notifications/my?page=1&pageSize=10"));
        var readItem = readPage.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.EndsWith("Z", readItem.GetProperty("readAt").GetString());
        Assert.Equal(1, (await Data(await owner.GetAsync("/api/notifications/unread-count"))).GetProperty("unreadCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await Data(await owner.GetAsync("/api/notifications/my?unreadOnly=true"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await Data(await other.GetAsync("/api/notifications/unread-count"))).GetProperty("unreadCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/notifications/my?page=0")).StatusCode);
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
