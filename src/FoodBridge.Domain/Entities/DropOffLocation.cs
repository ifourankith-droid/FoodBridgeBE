using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

public sealed class DropOffLocation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Admin-curated, or discovered by a volunteer at delivery time.</summary>
    public DropOffLocationSource Source { get; set; }

    /// <summary>Who added it. Null for the admin-seeded rows that predate this column.</summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
