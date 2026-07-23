using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;
using FoodBridge.Domain.StateMachines;

namespace FoodBridge.Application.Listings;

public sealed class ListingService : IListingService
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png" };

    private readonly IListingRepository _listingRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ListingService(IListingRepository listingRepository, IFileStorage fileStorage, ICurrentUser currentUser, IClock clock)
    {
        _listingRepository = listingRepository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var listing = new Listing
        {
            DonorId = _currentUser.UserId,
            Title = request.Title,
            FoodType = request.FoodType,
            DietType = ParseNullableEnum<DietType>(request.DietType),
            MealType = ParseNullableEnum<MealType>(request.MealType),
            QuantityMeals = request.QuantityMeals,
            FreshnessTag = Enum.Parse<FreshnessTag>(request.FreshnessTag, true),
            PreparedAtUtc = request.PreparedAtUtc,
            PickupDeadlineUtc = request.PickupDeadlineUtc,
            PickupAddress = request.PickupAddress,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = ListingStatus.Pending,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var creationEvent = new ListingTimelineEvent
        {
            FromStatus = null,
            ToStatus = ListingStatus.Pending,
            ActorUserId = _currentUser.UserId,
            Note = "Listing created.",
            CreatedAtUtc = now,
        };

        await _listingRepository.CreateAsync(listing, creationEvent, cancellationToken);

        return Result.Success(listing.ToResponse(Array.Empty<ListingImage>(), new[] { creationEvent }), "Listing created successfully.");
    }

    public async Task<Result<PagedResult<ListingSummaryResponse>>> GetMyListingsAsync(int page, int pageSize, string? status, CancellationToken cancellationToken = default)
    {
        ListingStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ListingStatus>(status, true, out var parsed))
            {
                return Result.Failure<PagedResult<ListingSummaryResponse>>($"Unknown status '{status}'.");
            }

            statusFilter = parsed;
        }

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetByDonorAsync(_currentUser.UserId, statusFilter, normalizedPage, normalizedPageSize, cancellationToken);

        var summaries = items.Select(l => l.ToSummaryResponse()).ToList();
        return Result.Success(new PagedResult<ListingSummaryResponse>(summaries, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<ListingResponse>> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingOrThrowAsync(listingId, cancellationToken);
        var images = await _listingRepository.GetImagesAsync(listingId, cancellationToken);
        var timeline = await _listingRepository.GetTimelineAsync(listingId, cancellationToken);

        return Result.Success(listing.ToResponse(images, timeline));
    }

    public async Task<Result<ListingResponse>> UpdateAsync(Guid listingId, UpdateListingRequest request, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingOrThrowAsync(listingId, cancellationToken);
        EnsurePending(listing, "Only pending listings can be edited.");

        listing.Title = request.Title;
        listing.FoodType = request.FoodType;
        listing.DietType = ParseNullableEnum<DietType>(request.DietType);
        listing.MealType = ParseNullableEnum<MealType>(request.MealType);
        listing.QuantityMeals = request.QuantityMeals;
        listing.FreshnessTag = Enum.Parse<FreshnessTag>(request.FreshnessTag, true);
        listing.PreparedAtUtc = request.PreparedAtUtc;
        listing.PickupDeadlineUtc = request.PickupDeadlineUtc;
        listing.PickupAddress = request.PickupAddress;
        listing.Latitude = request.Latitude;
        listing.Longitude = request.Longitude;
        listing.UpdatedAtUtc = _clock.UtcNow;

        await _listingRepository.UpdateAsync(listing, cancellationToken);

        var images = await _listingRepository.GetImagesAsync(listingId, cancellationToken);
        var timeline = await _listingRepository.GetTimelineAsync(listingId, cancellationToken);
        return Result.Success(listing.ToResponse(images, timeline), "Listing updated successfully.");
    }

    public async Task<Result<ListingResponse>> CancelAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingOrThrowAsync(listingId, cancellationToken);
        ListingStateMachine.EnsureCanTransition(listing.Status, ListingStatus.Cancelled);

        var now = _clock.UtcNow;
        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = ListingStatus.Cancelled,
            ActorUserId = _currentUser.UserId,
            Note = "Cancelled by donor.",
            CreatedAtUtc = now,
        };

        listing.Status = ListingStatus.Cancelled;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ChangeStatusAsync(listing, timelineEvent, cancellationToken);

        var images = await _listingRepository.GetImagesAsync(listingId, cancellationToken);
        var timeline = await _listingRepository.GetTimelineAsync(listingId, cancellationToken);
        return Result.Success(listing.ToResponse(images, timeline), "Listing cancelled successfully.");
    }

    public async Task<Result<ListingImageUploadResponse>> UploadImageAsync(Guid listingId, Stream fileContent, string fileExtension, long fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingOrThrowAsync(listingId, cancellationToken);
        EnsurePending(listing, "Images can only be added to pending listings.");

        if (fileSizeBytes > MaxImageSizeBytes)
        {
            return Result.Failure<ListingImageUploadResponse>("Image must be 5MB or smaller.");
        }

        if (!AllowedImageExtensions.Contains(fileExtension.ToLowerInvariant()))
        {
            return Result.Failure<ListingImageUploadResponse>("Image must be a JPG or PNG file.");
        }

        var imageUrl = await _fileStorage.SaveAsync(fileContent, fileExtension.ToLowerInvariant(), cancellationToken);

        var now = _clock.UtcNow;
        var image = new ListingImage { ListingId = listingId, ImageUrl = imageUrl, CreatedAtUtc = now, UpdatedAtUtc = now };
        var imageId = await _listingRepository.AddImageAsync(image, cancellationToken);

        return Result.Success(new ListingImageUploadResponse(imageId, imageUrl), "Image uploaded successfully.");
    }

    private static TEnum? ParseNullableEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<TEnum>(value, true);

    private static void EnsurePending(Listing listing, string message)
    {
        if (listing.Status != ListingStatus.Pending)
        {
            throw new BusinessRuleException(message);
        }
    }

    private async Task<Listing> GetOwnedListingOrThrowAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        if (listing.DonorId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only access your own listings.");
        }

        return listing;
    }
}
