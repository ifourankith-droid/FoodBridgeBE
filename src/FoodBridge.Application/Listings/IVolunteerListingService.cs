using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;

namespace FoodBridge.Application.Listings;

public interface IVolunteerListingService
{
    Task<Result<PagedResult<ListingNearbyResponse>>> GetNearbyAsync(decimal latitude, decimal longitude, double? radiusKm, string? dietType, string? mealType, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// The signed-in volunteer's claimed listings across every stage (Claimed → Confirmed),
    /// most recently updated first — the data behind the My Deliveries page.
    /// </summary>
    Task<Result<PagedResult<ListingResponse>>> GetMyDeliveriesAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pending → Claimed. Any available volunteer may claim; exactly one wins under a
    /// concurrent race (409 for the loser). The optional <paramref name="estimatedPickupAtUtc"/>
    /// lets the volunteer commit to a delayed pickup instead of an implied immediate one;
    /// must be in the future and no later than the listing's own PickupDeadlineUtc (422 otherwise).
    /// </summary>
    Task<Result<ListingResponse>> ClaimAsync(Guid listingId, DateTime? estimatedPickupAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Claimed → Pending. Assigned volunteer only; voluntarily releases a listing back for someone else to claim.</summary>
    Task<Result<ListingResponse>> UnclaimAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Claimed → PickedUp. Assigned volunteer only; requires a photo; auto-matches a recipient via <see cref="IRecipientMatcher"/> if none is set yet.</summary>
    Task<Result<ListingResponse>> ConfirmPickupAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>PickedUp → Delivered. Assigned volunteer only; requires a photo and a previously matched recipient.</summary>
    /// <param name="dropOff">
    /// Where the food was dropped. Required — recording it is what builds the shared pool of
    /// recipient hotspots, and a delivery with no destination can't be audited. A brand-new spot
    /// is created and logged in the same transaction as the delivery.
    /// </param>
    Task<Result<ListingResponse>> ConfirmDeliveryAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, DropOffChoice dropOff, CancellationToken cancellationToken = default);
}
