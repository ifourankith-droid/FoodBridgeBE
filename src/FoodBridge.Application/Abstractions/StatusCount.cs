namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection — a raw status byte (interpreted by the caller as either ListingStatus or AccountStatus) with its row count.</summary>
public sealed class StatusCount
{
    public byte Status { get; set; }
    public int Count { get; set; }
}
