namespace FoodBridge.Application.Listings;

public interface IRecipientMatcher
{
    /// <summary>
    /// Returns the id of the nearest available, Verified recipient not in
    /// <paramref name="excludeRecipientIds"/>, or null if none exists. Recipient-reject
    /// passes every recipient who has already rejected this same listing (plus the
    /// current one) — otherwise, with only two available recipients, rejects would
    /// ping-pong between them forever instead of ever reaching "no recipient available".
    /// </summary>
    Task<Guid?> FindNearestAvailableRecipientAsync(decimal latitude, decimal longitude, IReadOnlyCollection<Guid>? excludeRecipientIds = null, CancellationToken cancellationToken = default);
}
