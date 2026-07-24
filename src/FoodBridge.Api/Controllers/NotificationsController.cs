using FoodBridge.Application.Common;
using FoodBridge.Application.Notifications;
using FoodBridge.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// REST fallback for notifications delivered live via NotificationsHub. Any authenticated role.
/// </summary>
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : BaseController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Lists the caller's own notifications, optionally filtered by read status.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<NotificationResponse>>> GetMyNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _notificationService.GetMyNotificationsAsync(isRead, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>Marks a notification read. Self only.</summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.MarkAsReadAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
