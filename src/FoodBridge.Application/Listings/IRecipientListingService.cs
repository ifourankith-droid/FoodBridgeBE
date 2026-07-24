using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;

namespace FoodBridge.Application.Listings;

public interface IRecipientListingService
{
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
