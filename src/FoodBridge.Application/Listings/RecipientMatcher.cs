using FoodBridge.Application.Abstractions;

namespace FoodBridge.Application.Listings;

public sealed class RecipientMatcher : IRecipientMatcher
{
    private readonly IRecipientReader _recipientReader;

    public RecipientMatcher(IRecipientReader recipientReader)
    {
        _recipientReader = recipientReader;
    }

    public Task<Guid?> FindNearestAvailableRecipientAsync(decimal latitude, decimal longitude, IReadOnlyCollection<Guid>? excludeRecipientIds = null, CancellationToken cancellationToken = default) =>
        _recipientReader.FindNearestAvailableRecipientIdAsync(latitude, longitude, excludeRecipientIds, cancellationToken);
}
