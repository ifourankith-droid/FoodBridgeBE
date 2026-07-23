namespace FoodBridge.Application.Listings;

public interface IRecipientMatcher
{
    /// <summary>Returns the id of the nearest available, Verified recipient, or null if none exists.</summary>
    Task<Guid?> FindNearestAvailableRecipientAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
}
