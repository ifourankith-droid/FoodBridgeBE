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

    /// <summary>Matches VolunteerListingService's default nearby-search radius, so a volunteer is pushed for exactly the listings they'd otherwise find via GET /api/listings/nearby with no radiusKm override.</summary>
    private const double NotifyVolunteersRadiusKm = 10;

    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDonorAddressRepository _donorAddressRepository;
    private readonly IFileStorage _fileStorage;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ListingService(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        IDonorAddressRepository donorAddressRepository,
        IFileStorage fileStorage,
        INotificationDispatcher notificationDispatcher,
        ICurrentUser currentUser,
        IClock clock)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _donorAddressRepository = donorAddressRepository;
        _fileStorage = fileStorage;
        _notificationDispatcher = notificationDispatcher;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken = default)
    {
        string pickupAddress;
        decimal latitude;
        decimal longitude;

        if (request.DonorAddressId.HasValue)
        {
            var savedAddress = await _donorAddressRepository.GetByIdAsync(request.DonorAddressId.Value, cancellationToken);
            if (savedAddress is null)
            {
                throw new NotFoundException("DonorAddress", request.DonorAddressId.Value);
            }

            if (savedAddress.DonorId != _currentUser.UserId)
            {
                throw new UnauthorizedAccessException("You can only use your own saved addresses.");
            }

            pickupAddress = savedAddress.Address;
            latitude = savedAddress.Latitude;
            longitude = savedAddress.Longitude;
        }
        else
        {
            // Validator already guarantees these are set when DonorAddressId isn't.
            pickupAddress = request.PickupAddress!;
            latitude = request.Latitude!.Value;
            longitude = request.Longitude!.Value;
        }

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
            PickupAddress = pickupAddress,
            Latitude = latitude,
            Longitude = longitude,
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

        var nearbyVolunteerIds = await _userRepository.GetNearbyAvailableVolunteerIdsAsync(latitude, longitude, NotifyVolunteersRadiusKm * 1000, cancellationToken);
        var volunteerNotifications = nearbyVolunteerIds.Select(volunteerId => new Notification
        {
            UserId = volunteerId,
            Type = "NewListingNearby",
            Title = "New pickup available near you",
            Body = $"'{listing.Title}' — {listing.QuantityMeals} meals ready for pickup near {pickupAddress}.",
            IsRead = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }).ToList();

        await _listingRepository.CreateAsync(listing, creationEvent, volunteerNotifications, cancellationToken);

        // Best-effort live push, after the atomic write has already committed — same
        // reasoning as RecipientListingService.ConfirmReceiptAsync: a dispatch failure
        // must never roll back a listing that was already created successfully.
        foreach (var notification in volunteerNotifications)
        {
            await _notificationDispatcher.DispatchAsync(notification, cancellationToken);
        }

        return Result.Success(await listing.ToResponseAsync(Array.Empty<ListingImage>(), new[] { creationEvent }, _userRepository, cancellationToken), "Listing created successfully.");
    }

    public async Task<Result<PagedResult<ListingSummaryResponse>>> GetMyListingsAsync(int page, int pageSize, string? status, string? dietType, string? mealType, CancellationToken cancellationToken = default)
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

        DietType? dietFilter = null;
        if (!string.IsNullOrWhiteSpace(dietType))
        {
            if (!Enum.TryParse<DietType>(dietType, true, out var parsedDiet))
            {
                return Result.Failure<PagedResult<ListingSummaryResponse>>($"Unknown dietType '{dietType}'.");
            }

            dietFilter = parsedDiet;
        }

        MealType? mealFilter = null;
        if (!string.IsNullOrWhiteSpace(mealType))
        {
            if (!Enum.TryParse<MealType>(mealType, true, out var parsedMeal))
            {
                return Result.Failure<PagedResult<ListingSummaryResponse>>($"Unknown mealType '{mealType}'.");
            }

            mealFilter = parsedMeal;
        }

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetByDonorAsync(_currentUser.UserId, statusFilter, dietFilter, mealFilter, normalizedPage, normalizedPageSize, cancellationToken);

        var summaries = items.Select(l => l.ToSummaryResponse()).ToList();
        return Result.Success(new PagedResult<ListingSummaryResponse>(summaries, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<ListingResponse>> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingOrThrowAsync(listingId, cancellationToken);
        var images = await _listingRepository.GetImagesAsync(listingId, cancellationToken);
        var timeline = await _listingRepository.GetTimelineAsync(listingId, cancellationToken);

        return Result.Success(await listing.ToResponseAsync(images, timeline, _userRepository, cancellationToken));
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
        return Result.Success(await listing.ToResponseAsync(images, timeline, _userRepository, cancellationToken), "Listing updated successfully.");
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
        return Result.Success(await listing.ToResponseAsync(images, timeline, _userRepository, cancellationToken), "Listing cancelled successfully.");
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
