using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;

namespace FoodBridge.Application.Listings;

public interface IRecipientListingService
{
    /// <summary>
    /// Donations still up for grabs near the given point — Pending or Claimed, unmatched
    /// (or already requested by the caller), deadline not passed — ordered by distance.
    /// This is the pull-side counterpart to the automatic nearest-recipient match: it lets
    /// an NGO see what is around it instead of only hearing about a donation once a
    /// volunteer has already collected it.
    /// </summary>
    Task<Result<PagedResult<ListingAvailableNearbyResponse>>> GetAvailableNearbyAsync(decimal latitude, decimal longitude, double? radiusKm, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves an unmatched nearby donation for the caller, so the volunteer's
    /// confirm-pickup routes it here instead of running the nearest-available matcher.
    /// Status is unchanged; the normal accept/reject decision still happens once the food
    /// is actually collected.
    /// </summary>
    Task<Result<ListingResponse>> RequestAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Releases a request made via <see cref="RequestAsync"/>, while the food is still uncollected.</summary>
    Task<Result<ListingResponse>> WithdrawRequestAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Listings currently matched to the caller and awaiting accept/reject (Status = PickedUp).</summary>
    Task<Result<PagedResult<ListingSummaryResponse>>> GetIncomingAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Acknowledges the match. Doesn't change Status — just records the acceptance.</summary>
    Task<Result<ListingResponse>> AcceptAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Declines the match; auto-reassigns to another available recipient via <see cref="IRecipientMatcher"/>, or clears RecipientId if none exists. Status stays PickedUp.</summary>
    Task<Result<ListingResponse>> RejectAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Delivered → Confirmed, atomically awarding volunteer points, issuing a certificate, and creating notifications.</summary>
    Task<Result<ConfirmReceiptResponse>> ConfirmReceiptAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>The caller's past confirmed receipts (Status = Confirmed).</summary>
    Task<Result<PagedResult<ListingSummaryResponse>>> GetHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
