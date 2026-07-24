using FoodBridge.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FoodBridge.Api.Hubs;

/// <summary>
/// Live notification delivery. Each connection joins a per-user group on connect, so
/// <see cref="Notifications.SignalRNotificationDispatcher"/> can push to exactly that
/// user's connected clients (including multiple tabs/devices at once) via
/// <c>Clients.Group(GroupName(userId))</c>.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    private readonly ICurrentUser _currentUser;

    public NotificationsHub(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(_currentUser.UserId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(_currentUser.UserId));
        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(Guid userId) => $"user:{userId}";
}
