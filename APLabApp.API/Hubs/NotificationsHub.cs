using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace APLabApp.API.Hubs
{
  

    [Authorize]
    public class NotificationsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var sub = Context.User?.FindFirst("sub")?.Value?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(sub))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"kc:{sub}");

            var roles = Context.User?.FindAll("roles")?.Select(c => c.Value) ?? Enumerable.Empty<string>();
            if (roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
                await Groups.AddToGroupAsync(Context.ConnectionId, "role:admin");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var sub = Context.User?.FindFirst("sub")?.Value?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(sub))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"kc:{sub}");

            var roles = Context.User?.FindAll("roles")?.Select(c => c.Value) ?? Enumerable.Empty<string>();
            if (roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "role:admin");

            await base.OnDisconnectedAsync(exception);
        }

    }
}
