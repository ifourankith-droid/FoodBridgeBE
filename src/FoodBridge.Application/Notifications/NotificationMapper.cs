using FoodBridge.Application.Notifications.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Notifications;

public static class NotificationMapper
{
    public static NotificationResponse ToResponse(this Notification notification) => new(
        notification.Id,
        notification.Type,
        notification.Title,
        notification.Body,
        notification.PayloadJson,
        notification.IsRead,
        notification.CreatedAtUtc);
}
