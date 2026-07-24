using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

public sealed class Dispute
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Guid RaisedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
