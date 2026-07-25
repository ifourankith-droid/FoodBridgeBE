using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;

namespace FoodBridge.Application.Listings;

public interface IVolunteerListingService
{
    Task<Result<PagedResult<ListingNearbyResponse>>> GetNearbyAsync(decimal latitude, decimal longitude, double? radiusKm, string? dietType, string? mealType, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Pending → Claimed. Any available volunteer may claim; exactly one wins under a concurrent race (409 for the loser).</summary>
    Task<Result<ListingResponse>> ClaimAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Claimed → Pending. Assigned volunteer only; voluntarily releases a listing back for someone else to claim.</summary>
    Task<Result<ListingResponse>> UnclaimAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Claimed → PickedUp. Assigned volunteer only; requires a photo; auto-matches a recipient via <see cref="IRecipientMatcher"/> if none is set yet.</summary>
    Task<Result<ListingResponse>> ConfirmPickupAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>PickedUp → Delivered. Assigned volunteer only; requires a photo and a previously matched recipient.</summary>
    Task<Result<ListingResponse>> ConfirmDeliveryAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, CancellationToken cancellationToken = default);
}
