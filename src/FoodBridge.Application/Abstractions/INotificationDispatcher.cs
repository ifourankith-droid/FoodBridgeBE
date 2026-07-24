using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Pushes an already-persisted notification to the recipient's connected clients in
/// real time. Best-effort — if the user isn't connected, they'll see it via the
/// GET /api/notifications REST fallback instead. Swap the SignalR implementation for
/// another provider (e.g. push notifications) without touching callers.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(Notification notification, CancellationToken cancellationToken = default);
}
