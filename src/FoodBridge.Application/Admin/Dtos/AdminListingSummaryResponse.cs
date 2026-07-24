namespace FoodBridge.Application.Admin.Dtos;

public sealed record AdminListingSummaryResponse(
    Guid Id,
    string Title,
    string Status,
    Guid DonorId,
    string DonorName,
    Guid? VolunteerId,
    Guid? RecipientId,
    int QuantityMeals,
    DateTime PickupDeadlineUtc,
    DateTime CreatedAtUtc);
