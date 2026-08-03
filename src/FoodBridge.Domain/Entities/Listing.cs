using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

public sealed class Listing
{
    public Guid Id { get; set; }
    public Guid DonorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FoodType { get; set; } = string.Empty;
    public DietType? DietType { get; set; }
    public MealType? MealType { get; set; }
    public int QuantityMeals { get; set; }
    public FreshnessTag FreshnessTag { get; set; }
    public DateTime? PreparedAtUtc { get; set; }
    public DateTime PickupDeadlineUtc { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public ListingStatus Status { get; set; }
    public Guid? VolunteerId { get; set; }
    public Guid? RecipientId { get; set; }
    public DateTime? EstimatedPickupAtUtc { get; set; }

    /// <summary>
    /// When the donor confirmed the food is safe to eat and that its quality is their
    /// responsibility. Set at creation and never changed. Null only for listings that predate the
    /// declaration being required — never for one that skipped it.
    /// </summary>
    public DateTime? FoodSafetyAcceptedAtUtc { get; set; }

    /// <summary>
    /// When the donor was warned that half the pickup window had elapsed with no volunteer, and
    /// offered the option to deliver it themselves. Null means either not yet half-way, or the
    /// listing left Pending before it got there.
    /// </summary>
    public DateTime? HalfwayNoticeSentAtUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
