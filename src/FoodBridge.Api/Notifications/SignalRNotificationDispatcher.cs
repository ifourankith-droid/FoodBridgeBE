using FoodBridge.Api.Hubs;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Notifications;
using FoodBridge.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace FoodBridge.Api.Notifications;

/// <summary>
/// Lives in Api (not Infrastructure) because it depends on <see cref="NotificationsHub"/>,
/// an ASP.NET Core SignalR endpoint — the same reason Controllers live in Api rather than
/// Infrastructure. Registered against <see cref="INotificationDispatcher"/> in Program.cs
/// like every other provider.
/// </summary>
public sealed class SignalRNotificationDispatcher : INotificationDispatcher
{
    private readonly IHubContext<NotificationsHub> _hubContext;

    public SignalRNotificationDispatcher(IHubContext<NotificationsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task DispatchAsync(Notification notification, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(NotificationsHub.GroupName(notification.UserId))
            .SendAsync("ReceiveNotification", notification.ToResponse(), cancellationToken);
}
