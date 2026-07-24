using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection for the admin listings browse — includes the donor's name, unlike the donor's own ListingSummaryResponse.</summary>
public sealed class AdminListingSummary
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ListingStatus Status { get; set; }
    public Guid DonorId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public Guid? VolunteerId { get; set; }
    public Guid? RecipientId { get; set; }
    public int QuantityMeals { get; set; }
    public DateTime PickupDeadlineUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
