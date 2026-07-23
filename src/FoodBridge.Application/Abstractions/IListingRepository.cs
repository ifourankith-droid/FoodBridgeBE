using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

public interface IListingRepository
{
    /// <summary>
    /// Inserts the listing and its creation timeline event in one transaction.
    /// Mutates <paramref name="listing"/>.Id and <paramref name="creationEvent"/>.ListingId
    /// with the generated id.
    /// </summary>
    Task<Guid> CreateAsync(Listing listing, ListingTimelineEvent creationEvent, CancellationToken cancellationToken = default);

    Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetByDonorAsync(Guid donorId, ListingStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingImage>> GetImagesAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingTimelineEvent>> GetTimelineAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Listing listing, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the listing's status and inserts the corresponding timeline event
    /// in one transaction.
    /// </summary>
    Task ChangeStatusAsync(Listing listing, ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default);

    Task<Guid> AddImageAsync(ListingImage image, CancellationToken cancellationToken = default);
}
