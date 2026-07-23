using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;

namespace FoodBridge.Application.Listings;

public interface IListingService
{
    Task<Result<ListingResponse>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ListingSummaryResponse>>> GetMyListingsAsync(int page, int pageSize, string? status, CancellationToken cancellationToken = default);

    Task<Result<ListingResponse>> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Only permitted while the listing is Pending.</summary>
    Task<Result<ListingResponse>> UpdateAsync(Guid listingId, UpdateListingRequest request, CancellationToken cancellationToken = default);

    Task<Result<ListingResponse>> CancelAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>Only permitted while the listing is Pending.</summary>
    Task<Result<ListingImageUploadResponse>> UploadImageAsync(Guid listingId, Stream fileContent, string fileExtension, long fileSizeBytes, CancellationToken cancellationToken = default);
}
