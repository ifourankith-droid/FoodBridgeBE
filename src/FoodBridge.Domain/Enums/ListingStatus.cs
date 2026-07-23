namespace FoodBridge.Domain.Enums;

public enum ListingStatus : byte
{
    Pending = 1,
    Claimed = 2,
    PickedUp = 3,
    Delivered = 4,
    Confirmed = 5,
    Expired = 6,
    Cancelled = 7,
    Rejected = 8,
}
