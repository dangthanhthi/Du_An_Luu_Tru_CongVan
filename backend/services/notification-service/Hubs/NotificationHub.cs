using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace NotificationService.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)??Context.User?.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{Guid.Parse(userId):D}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var httpContext = Context.GetHttpContext();
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)??Context.User?.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{Guid.Parse(userId):D}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
