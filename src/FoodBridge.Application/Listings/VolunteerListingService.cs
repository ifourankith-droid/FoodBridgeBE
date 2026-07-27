using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;
using FoodBridge.Domain.StateMachines;

namespace FoodBridge.Application.Listings;

public sealed class VolunteerListingService : IVolunteerListingService
{
    private const double DefaultRadiusKm = 10;
    private const double MaxRadiusKm = 50;
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png" };

    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRecipientMatcher _recipientMatcher;
    private readonly IDropOffLocationRepository _dropOffLocationRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public VolunteerListingService(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        IRecipientMatcher recipientMatcher,
        IDropOffLocationRepository dropOffLocationRepository,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        IClock clock)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _recipientMatcher = recipientMatcher;
        _dropOffLocationRepository = dropOffLocationRepository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PagedResult<ListingNearbyResponse>>> GetNearbyAsync(decimal latitude, decimal longitude, double? radiusKm, string? dietType, string? mealType, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (latitude < -90 || latitude > 90)
        {
            return Result.Failure<PagedResult<ListingNearbyResponse>>("Latitude must be between -90 and 90.");
        }

        if (longitude < -180 || longitude > 180)
        {
            return Result.Failure<PagedResult<ListingNearbyResponse>>("Longitude must be between -180 and 180.");
        }

        DietType? dietFilter = null;
        if (!string.IsNullOrWhiteSpace(dietType))
        {
            if (!Enum.TryParse<DietType>(dietType, true, out var parsedDiet))
            {
                return Result.Failure<PagedResult<ListingNearbyResponse>>($"Unknown dietType '{dietType}'.");
            }

            dietFilter = parsedDiet;
        }

        MealType? mealFilter = null;
        if (!string.IsNullOrWhiteSpace(mealType))
        {
            if (!Enum.TryParse<MealType>(mealType, true, out var parsedMeal))
            {
                return Result.Failure<PagedResult<ListingNearbyResponse>>($"Unknown mealType '{mealType}'.");
            }

            mealFilter = parsedMeal;
        }

        var effectiveRadiusKm = radiusKm switch
        {
            null or <= 0 => DefaultRadiusKm,
            > MaxRadiusKm => MaxRadiusKm,
            _ => radiusKm.Value,
        };

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetNearbyPendingAsync(latitude, longitude, effectiveRadiusKm * 1000, dietFilter, mealFilter, normalizedPage, normalizedPageSize, cancellationToken);

        var responses = items
            .Select(i => new ListingNearbyResponse(
                i.Id,
                i.Title,
                i.FoodType,
                i.DietType?.ToString(),
                i.MealType?.ToString(),
                i.QuantityMeals,
                i.FreshnessTag.ToString(),
                i.PickupDeadlineUtc,
                i.PickupAddress,
                i.Latitude,
                i.Longitude,
                Math.Round(i.DistanceMeters / 1000, 2)))
            .ToList();

        return Result.Success(new PagedResult<ListingNearbyResponse>(responses, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<ListingResponse>> ClaimAsync(Guid listingId, DateTime? estimatedPickupAtUtc, CancellationToken cancellationToken = default)
    {
        var volunteerId = _currentUser.UserId;
        var now = _clock.UtcNow;

        if (estimatedPickupAtUtc.HasValue)
        {
            if (estimatedPickupAtUtc.Value <= now)
            {
                return Result.Failure<ListingResponse>("EstimatedPickupAtUtc must be in the future.");
            }

            var listingToClaim = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
            if (listingToClaim is null)
            {
                throw new NotFoundException("Listing", listingId);
            }

            if (estimatedPickupAtUtc.Value > listingToClaim.PickupDeadlineUtc)
            {
                return Result.Failure<ListingResponse>("EstimatedPickupAtUtc cannot be later than the listing's pickup deadline.");
            }
        }

        var claimEvent = new ListingTimelineEvent
        {
            FromStatus = ListingStatus.Pending,
            ToStatus = ListingStatus.Claimed,
            ActorUserId = volunteerId,
            Note = estimatedPickupAtUtc.HasValue
                ? $"Claimed by volunteer; estimated pickup at {estimatedPickupAtUtc:u}."
                : "Claimed by volunteer.",
            CreatedAtUtc = now,
        };

        var claimed = await _listingRepository.TryClaimAsync(listingId, volunteerId, estimatedPickupAtUtc, claimEvent, cancellationToken);
        if (!claimed)
        {
            var existing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
            if (existing is null)
            {
                throw new NotFoundException("Listing", listingId);
            }

            throw new ConflictException($"Listing is no longer available to claim (current status: {existing.Status}).");
        }

        return Result.Success(await BuildResponseAsync(listingId, cancellationToken), "Listing claimed successfully.");
    }

    public async Task<Result<ListingResponse>> UnclaimAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetAssignedListingOrThrowAsync(listingId, cancellationToken);
        ListingStateMachine.EnsureCanTransition(listing.Status, ListingStatus.Pending);

        var now = _clock.UtcNow;
        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = ListingStatus.Pending,
            ActorUserId = _currentUser.UserId,
            Note = "Volunteer released the claim; listing is available again.",
            CreatedAtUtc = now,
        };

        listing.Status = ListingStatus.Pending;
        listing.VolunteerId = null;
        listing.EstimatedPickupAtUtc = null;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ChangeStatusAsync(listing, timelineEvent, cancellationToken);

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Claim released successfully.");
    }

    public async Task<Result<ListingResponse>> ConfirmPickupAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, CancellationToken cancellationToken = default)
    {
        var listing = await GetAssignedListingOrThrowAsync(listingId, cancellationToken);
        ListingStateMachine.EnsureCanTransition(listing.Status, ListingStatus.PickedUp);

        var photoValidation = ValidatePhoto(photoSizeBytes, photoExtension);
        if (photoValidation is not null)
        {
            return Result.Failure<ListingResponse>(photoValidation);
        }

        var photoUrl = await _fileStorage.SaveAsync(photoContent, photoExtension.ToLowerInvariant(), cancellationToken);

        if (listing.RecipientId is null)
        {
            listing.RecipientId = await _recipientMatcher.FindNearestAvailableRecipientAsync(listing.Latitude, listing.Longitude, cancellationToken: cancellationToken);
        }

        var now = _clock.UtcNow;
        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = ListingStatus.PickedUp,
            ActorUserId = _currentUser.UserId,
            Note = listing.RecipientId is null ? "Picked up by volunteer. No recipient available yet." : "Picked up by volunteer.",
            PhotoUrl = photoUrl,
            CreatedAtUtc = now,
        };

        listing.Status = ListingStatus.PickedUp;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ChangeStatusAsync(listing, timelineEvent, cancellationToken);

        var response = await BuildResponseAsync(listing, cancellationToken);
        if (listing.RecipientId is null)
        {
            // The volunteer is the one calling this endpoint, so they learn the fallback
            // destination synchronously here — no separate notification needed, unlike
            // RecipientListingService.RejectAsync where a *different* user's action is
            // what exhausts the recipient search.
            var nearestDropOff = await _dropOffLocationRepository.GetNearestActiveAsync(listing.Latitude, listing.Longitude, cancellationToken);
            response = response with { SuggestedDropOffLocation = nearestDropOff?.ToResponse() };
        }

        return Result.Success(response, "Pickup confirmed successfully.");
    }

    public async Task<Result<ListingResponse>> ConfirmDeliveryAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, CancellationToken cancellationToken = default)
    {
        var listing = await GetAssignedListingOrThrowAsync(listingId, cancellationToken);
        ListingStateMachine.EnsureCanTransition(listing.Status, ListingStatus.Delivered);

        if (listing.RecipientId is null)
        {
            throw new BusinessRuleException("Cannot confirm delivery before a recipient has been matched.");
        }

        var photoValidation = ValidatePhoto(photoSizeBytes, photoExtension);
        if (photoValidation is not null)
        {
            return Result.Failure<ListingResponse>(photoValidation);
        }

        var photoUrl = await _fileStorage.SaveAsync(photoContent, photoExtension.ToLowerInvariant(), cancellationToken);

        var now = _clock.UtcNow;
        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = ListingStatus.Delivered,
            ActorUserId = _currentUser.UserId,
            Note = "Delivered by volunteer.",
            PhotoUrl = photoUrl,
            CreatedAtUtc = now,
        };

        listing.Status = ListingStatus.Delivered;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ChangeStatusAsync(listing, timelineEvent, cancellationToken);

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Delivery confirmed successfully.");
    }

    private static string? ValidatePhoto(long photoSizeBytes, string photoExtension)
    {
        if (photoSizeBytes > MaxPhotoSizeBytes)
        {
            return "Photo must be 5MB or smaller.";
        }

        if (!AllowedPhotoExtensions.Contains(photoExtension.ToLowerInvariant()))
        {
            return "Photo must be a JPG or PNG file.";
        }

        return null;
    }

    private async Task<Listing> GetAssignedListingOrThrowAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        if (listing.VolunteerId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only act on listings assigned to you.");
        }

        return listing;
    }

    private async Task<ListingResponse> BuildResponseAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken) ?? throw new NotFoundException("Listing", listingId);
        return await BuildResponseAsync(listing, cancellationToken);
    }

    private async Task<ListingResponse> BuildResponseAsync(Listing listing, CancellationToken cancellationToken)
    {
        var images = await _listingRepository.GetImagesAsync(listing.Id, cancellationToken);
        var timeline = await _listingRepository.GetTimelineAsync(listing.Id, cancellationToken);
        return await listing.ToResponseAsync(images, timeline, _userRepository, cancellationToken);
    }
}
