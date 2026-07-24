namespace FoodBridge.Application.Notifications.Dtos;

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? PayloadJson,
    bool IsRead,
    DateTime CreatedAtUtc);
