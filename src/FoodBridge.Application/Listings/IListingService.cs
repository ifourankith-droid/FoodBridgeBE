using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;

namespace FoodBridge.Application.Listings;

public interface IListingService
{
    Task<Result<ListingResponse>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ListingSummaryResponse>>> GetMyListingsAsync(int page, int pageSize, string? status, string? dietType, string? mealType, CancellationToken cancellationToken = default);

    Task<Result<ListingResponse>> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing's lifecycle timeline — each status change with the actor's name, the
    /// time, and any note / proof photo. Available to any party (donor / volunteer /
    /// recipient), so both sections can render the same steps from one endpoint.
    /// </summary>
    Task<Result<IReadOnlyList<ListingTimelineEventResponse>>> GetTimelineAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Only permitted while the listing is Pending.</summary>
    Task<Result<ListingResponse>> UpdateAsync(Guid listingId, UpdateListingRequest request, CancellationToken cancellationToken = default);

    Task<Result<ListingResponse>> CancelAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Only permitted while the listing is Pending.</summary>
    Task<Result<ListingImageUploadResponse>> UploadImageAsync(Guid listingId, Stream fileContent, string fileExtension, long fileSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// The donor delivers their own still-unclaimed listing (Pending → Confirmed), taking it to a
    /// drop-off point themselves rather than waiting for a volunteer who may never come.
    /// <para>
    /// Own listing only, and only while Pending — once a volunteer has claimed it, they are en route
    /// and the donor must not complete it out from under them (409). Issues the donor's certificate
    /// but no volunteer points, since no volunteer was involved.
    /// </para>
    /// </summary>
    Task<Result<ListingResponse>> SelfDeliverAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, DropOffChoice dropOff, CancellationToken cancellationToken = default);
}
