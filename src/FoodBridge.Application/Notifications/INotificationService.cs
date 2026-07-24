using FoodBridge.Application.Common;
using FoodBridge.Application.Notifications.Dtos;

namespace FoodBridge.Application.Notifications;

public interface INotificationService
{
    Task<Result<PagedResult<NotificationResponse>>> GetMyNotificationsAsync(bool? isRead, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Self only.</summary>
    Task<Result<NotificationResponse>> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
