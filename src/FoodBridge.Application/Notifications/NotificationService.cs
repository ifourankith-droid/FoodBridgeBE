using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Notifications.Dtos;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUser _currentUser;

    public NotificationService(INotificationRepository notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<NotificationResponse>>> GetMyNotificationsAsync(bool? isRead, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _notificationRepository.GetForUserAsync(_currentUser.UserId, isRead, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<NotificationResponse>(items.Select(n => n.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<NotificationResponse>> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            throw new NotFoundException("Notification", notificationId);
        }

        if (notification.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only mark your own notifications as read.");
        }

        if (!notification.IsRead)
        {
            await _notificationRepository.MarkAsReadAsync(notificationId, cancellationToken);
            notification.IsRead = true;
        }

        return Result.Success(notification.ToResponse());
    }
}
