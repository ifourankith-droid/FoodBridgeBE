using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Application.Users;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;
using FoodBridge.Domain.StateMachines;
using Microsoft.Extensions.Options;

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
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly FeatureSettings _features;
    private readonly DropOffSettings _dropOffSettings;

    public VolunteerListingService(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        IRecipientMatcher recipientMatcher,
        IDropOffLocationRepository dropOffLocationRepository,
        IFileStorage fileStorage,
        INotificationDispatcher notificationDispatcher,
        ICurrentUser currentUser,
        IClock clock,
        IOptions<FeatureSettings> features,
        IOptions<DropOffSettings> dropOffSettings)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _recipientMatcher = recipientMatcher;
        _dropOffLocationRepository = dropOffLocationRepository;
        _fileStorage = fileStorage;
        _notificationDispatcher = notificationDispatcher;
        _currentUser = currentUser;
        _clock = clock;
        _features = features.Value;
        _dropOffSettings = dropOffSettings.Value;
    }

    public async Task<Result<PagedResult<ListingNearbyResponse>>> GetNearbyAsync(decimal latitude, decimal longitude, double? radiusKm, string? dietType, string? mealType, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
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

        if (!TryResolveStatus(status, out var statusFilter))
        {
            return Result.Failure<PagedResult<ListingNearbyResponse>>($"Unknown status '{status}'.");
        }

        var effectiveRadiusKm = radiusKm switch
        {
            null or <= 0 => DefaultRadiusKm,
            > MaxRadiusKm => MaxRadiusKm,
            _ => radiusKm.Value,
        };

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetNearbyPendingAsync(latitude, longitude, effectiveRadiusKm * 1000, dietFilter, mealFilter, statusFilter, normalizedPage, normalizedPageSize, cancellationToken);

        // Attach each listing's primary photo as a card thumbnail — same approach as the donor list.
        var imageUrls = await _listingRepository.GetPrimaryImageUrlsAsync(items.Select(i => i.Id).ToList(), cancellationToken);
        var responses = items
            .Select(i => i.ToResponse(imageUrls.TryGetValue(i.Id, out var url) ? url : null))
            .ToList();

        return Result.Success(new PagedResult<ListingNearbyResponse>(responses, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<PagedResult<ListingResponse>>> GetMyDeliveriesAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var volunteerId = _currentUser.UserId;
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);

        var (items, totalCount) = await _listingRepository.GetByVolunteerAsync(volunteerId, null, normalizedPage, normalizedPageSize, cancellationToken);

        // Full detail per row: the contacts, ETA and status, the food thumbnail (images),
        // and the step-by-step lifecycle timeline. The timeline is included here so the
        // My Deliveries detail view can render every step from this one response, without
        // a follow-up call to GET /listings/{id}/timeline per delivery.
        var responses = new List<ListingResponse>(items.Count);
        foreach (var listing in items)
        {
            var images = await _listingRepository.GetImagesAsync(listing.Id, cancellationToken);
            var timeline = await _listingRepository.GetTimelineAsync(listing.Id, cancellationToken);
            responses.Add(await listing.ToResponseAsync(images, timeline, _userRepository, cancellationToken));
        }

        return Result.Success(new PagedResult<ListingResponse>(responses, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>
    /// Resolves the nearby feed's status filter. Accepts the frontend's display label
    /// "Posted" as an alias for <see cref="ListingStatus.Pending"/>, any real
    /// <see cref="ListingStatus"/> name, and null/blank (→ Pending, the default feed).
    /// Returns <c>false</c> for an unrecognised value so the caller can fail with a 422.
    /// </summary>
    private static bool TryResolveStatus(string? status, out ListingStatus resolved)
    {
        if (string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            resolved = ListingStatus.Pending;
            return true;
        }

        return Enum.TryParse(status, ignoreCase: true, out resolved);
    }

    public async Task<Result<ListingResponse>> ClaimAsync(Guid listingId, DateTime? estimatedPickupAtUtc, CancellationToken cancellationToken = default)
    {
        var volunteerId = _currentUser.UserId;
        var now = _clock.UtcNow;

        var blocked = await CheckCanTakeOnWorkAsync(cancellationToken);
        if (blocked is not null)
        {
            return Result.Failure<ListingResponse>(blocked);
        }

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

        // The donor needs to know someone is coming, and who. Built before the claim so it
        // can ride the same transaction — the repository only inserts it if the conditional
        // UPDATE actually won, so the loser of a two-volunteer race notifies nobody.
        var listingForNotify = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listingForNotify is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        var donorNotification = ListingNotifications.Claimed(
            listingForNotify,
            await GetVolunteerNameAsync(volunteerId, cancellationToken),
            estimatedPickupAtUtc,
            now);

        var claimed = await _listingRepository.TryClaimAsync(listingId, volunteerId, estimatedPickupAtUtc, claimEvent, donorNotification, cancellationToken);
        if (!claimed)
        {
            var existing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
            if (existing is null)
            {
                throw new NotFoundException("Listing", listingId);
            }

            throw new ConflictException($"Listing is no longer available to claim (current status: {existing.Status}).");
        }

        await DispatchAsync(donorNotification, cancellationToken);

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

        // Built before VolunteerId is cleared, and sent to the donor: their food is
        // perishable and now needs a different volunteer, so silence is the wrong answer.
        var donorNotification = ListingNotifications.Unclaimed(listing, now);

        listing.Status = ListingStatus.Pending;
        listing.VolunteerId = null;
        listing.EstimatedPickupAtUtc = null;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ChangeStatusAsync(listing, timelineEvent, new[] { donorNotification }, cancellationToken: cancellationToken);

        await DispatchAsync(donorNotification, cancellationToken);

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Claim released successfully.");
    }

    public async Task<Result<ListingResponse>> ConfirmPickupAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, CancellationToken cancellationToken = default)
    {
        var listing = await GetAssignedListingOrThrowAsync(listingId, cancellationToken);
        ListingStateMachine.EnsureCanTransition(listing.Status, ListingStatus.PickedUp);

        // Taking custody counts as new work. A volunteer suspended between claiming and collecting
        // should hand the listing back (unclaim), not walk away with the food.
        var blocked = await CheckCanTakeOnWorkAsync(cancellationToken);
        if (blocked is not null)
        {
            return Result.Failure<ListingResponse>(blocked);
        }

        var photoValidation = ValidatePhoto(photoSizeBytes, photoExtension);
        if (photoValidation is not null)
        {
            return Result.Failure<ListingResponse>(photoValidation);
        }

        var photoUrl = await _fileStorage.SaveAsync(photoContent, photoExtension.ToLowerInvariant(), cancellationToken);

        // With the Recipient role disabled, nothing should auto-assign a recipient — the
        // volunteer delivers to a drop-off location and completes the donation themselves.
        // A recipient the listing already has (they requested it proactively, or it was
        // matched while the role was enabled) is left alone, so in-flight donations keep
        // running the full accept/reject/confirm-receipt flow.
        if (listing.RecipientId is null && _features.RecipientRoleEnabled)
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
            Note = listing.RecipientId is null
                ? "Picked up by volunteer. Drop off at the suggested location."
                : "Picked up by volunteer.",
            PhotoUrl = photoUrl,
            CreatedAtUtc = now,
        };

        listing.Status = ListingStatus.PickedUp;
        listing.UpdatedAtUtc = now;

        // The donor's food has physically left their premises — the single most important
        // moment for them to hear about, and previously the point where they went silent.
        var donorNotification = ListingNotifications.PickedUp(
            listing,
            await GetVolunteerNameAsync(_currentUser.UserId, cancellationToken),
            now);

        await _listingRepository.ChangeStatusAsync(listing, timelineEvent, new[] { donorNotification }, cancellationToken: cancellationToken);

        await DispatchAsync(donorNotification, cancellationToken);

        var response = await BuildResponseAsync(listing, cancellationToken);
        if (listing.RecipientId is null)
        {
            // The volunteer is the one calling this endpoint, so they learn the fallback
            // destination synchronously here — no separate notification needed, unlike
            // RecipientListingService.RejectAsync where a *different* user's action is
            // what exhausts the recipient search.
            // Cooldown-aware: a spot that has just received food is not a useful suggestion,
            // so the nearest *available* one is offered instead.
            var nearestDropOff = await _dropOffLocationRepository.GetNearestAvailableAsync(
                listing.Latitude,
                listing.Longitude,
                now - TimeSpan.FromHours(_dropOffSettings.CooldownHours),
                cancellationToken);
            response = response with { SuggestedDropOffLocation = nearestDropOff?.ToResponse() };
        }

        return Result.Success(response, "Pickup confirmed successfully.");
    }

    public async Task<Result<ListingResponse>> ConfirmDeliveryAsync(Guid listingId, Stream photoContent, string photoExtension, long photoSizeBytes, DropOffChoice dropOff, CancellationToken cancellationToken = default)
    {
        var listing = await GetAssignedListingOrThrowAsync(listingId, cancellationToken);

        // A donation finishes one of two ways, decided by whether anyone is actually
        // waiting to receive it — never by the caller. With a matched recipient the
        // volunteer only reports Delivered and the recipient confirms receipt. With none
        // (the Recipient role is disabled, so nothing was matched) the food went to a
        // drop-off location and there is nobody left to confirm, so this call completes
        // the donation: points, certificate, and notifications, in one transaction.
        var completesDonation = listing.RecipientId is null;
        var targetStatus = completesDonation ? ListingStatus.Confirmed : ListingStatus.Delivered;
        ListingStateMachine.EnsureCanTransition(listing.Status, targetStatus);

        var photoValidation = ValidatePhoto(photoSizeBytes, photoExtension);
        if (photoValidation is not null)
        {
            return Result.Failure<ListingResponse>(photoValidation);
        }

        var now = _clock.UtcNow;

        // Resolved before the photo is written to storage: a rejected drop-off choice would
        // otherwise leave an orphaned file behind for a request that failed anyway.
        var dropOffResult = await ResolveDropOffAsync(listing, dropOff, now, cancellationToken);
        if (!dropOffResult.IsSuccess)
        {
            return Result.Failure<ListingResponse>(dropOffResult.Message);
        }

        var dropOffRecord = dropOffResult.Data!;
        var photoUrl = await _fileStorage.SaveAsync(photoContent, photoExtension.ToLowerInvariant(), cancellationToken);

        if (!completesDonation)
        {
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

            // No extra notification here: on this path the donor still gets DonationConfirmed
            // when the recipient confirms receipt, which is the outcome they care about.
            await _listingRepository.ChangeStatusAsync(listing, timelineEvent, dropOff: dropOffRecord, cancellationToken: cancellationToken);

            return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Delivery confirmed successfully.");
        }

        var completion = ListingCompletion.Build(
            listing,
            _currentUser.UserId,
            note: "Delivered to drop-off location and completed by volunteer.",
            photoUrl,
            now);

        listing.Status = ListingStatus.Confirmed;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ConfirmReceiptAsync(
            listing,
            completion.TimelineEvent,
            completion.VolunteerPoint,
            completion.Certificate,
            completion.Notifications,
            dropOffRecord,
            cancellationToken);

        // Best-effort live push, after the atomic write has already committed — a dispatch
        // failure must never roll back a completed donation. Same ordering as every other
        // notification in this app; GET /api/notifications is the fallback.
        foreach (var notification in completion.Notifications)
        {
            await _notificationDispatcher.DispatchAsync(notification, cancellationToken);
        }

        return Result.Success(
            await BuildResponseAsync(listing, cancellationToken),
            $"Delivery confirmed — donation completed and {completion.Points} points awarded.");
    }

    /// <summary>
    /// Turns the volunteer's drop-off choice into the rows to write: either a delivery pointed at
    /// an existing location, or a brand-new location plus its first delivery. Returns a failed
    /// <see cref="Result{T}"/> (→ 422) for an ambiguous, incomplete, or unusable choice rather
    /// than throwing, matching how every other expected business failure is reported here.
    /// </summary>
    private async Task<Result<DropOffRecord>> ResolveDropOffAsync(Listing listing, DropOffChoice dropOff, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (dropOff.IsExisting && (dropOff.Latitude.HasValue || dropOff.Longitude.HasValue || !string.IsNullOrWhiteSpace(dropOff.Name)))
        {
            return Result.Failure<DropOffRecord>("Provide either dropOffLocationId or a new location (latitude, longitude, locationName) — not both.");
        }

        var delivery = new DropOffDelivery
        {
            VolunteerId = _currentUser.UserId,
            ListingId = listing.Id,
            MealsCount = listing.QuantityMeals,
            DeliveredAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };

        if (dropOff.IsExisting)
        {
            var existing = await _dropOffLocationRepository.GetByIdAsync(dropOff.LocationId!.Value, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<DropOffRecord>("The selected drop-off location does not exist.");
            }

            if (!existing.IsActive)
            {
                return Result.Failure<DropOffRecord>("The selected drop-off location is no longer active. Please choose another.");
            }

            delivery.DropOffLocationId = existing.Id;
            return Result.Success(new DropOffRecord(delivery));
        }

        // Not an existing location, so it has to be a complete new one. Report the specific
        // missing piece rather than a blanket "invalid", since this is a form the volunteer fills.
        if (!dropOff.Latitude.HasValue || !dropOff.Longitude.HasValue)
        {
            return Result.Failure<DropOffRecord>("Where did you drop it off? Provide dropOffLocationId, or latitude and longitude for a new location.");
        }

        if (string.IsNullOrWhiteSpace(dropOff.Name))
        {
            return Result.Failure<DropOffRecord>("A new drop-off location needs a name.");
        }

        if (dropOff.Latitude is < -90 or > 90)
        {
            return Result.Failure<DropOffRecord>("Latitude must be between -90 and 90.");
        }

        if (dropOff.Longitude is < -180 or > 180)
        {
            return Result.Failure<DropOffRecord>("Longitude must be between -180 and 180.");
        }

        var name = dropOff.Name!.Trim();
        var newLocation = new DropOffLocation
        {
            Name = name,
            // The address is optional for a field-discovered spot — often there isn't one, and
            // the coordinates are the part that actually matters for routing.
            Address = string.IsNullOrWhiteSpace(dropOff.Address) ? name : dropOff.Address!.Trim(),
            Latitude = dropOff.Latitude.Value,
            Longitude = dropOff.Longitude.Value,
            City = null,
            IsActive = true,
            Source = DropOffLocationSource.Volunteer,
            CreatedByUserId = _currentUser.UserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

        return Result.Success(new DropOffRecord(delivery, newLocation));
    }

    /// <summary>
    /// Refuses the action unless this volunteer has passed admin verification, returning the
    /// blocking message or null when they're clear.
    /// <para>
    /// Applied to <b>claim</b> and <b>confirm-pickup</b> only — the two operations that take on new
    /// custody of food. Deliberately <em>not</em> applied to confirm-delivery or unclaim: a volunteer
    /// suspended while already carrying food must still be able to record where it went (blocking
    /// that strands the food with no audit trail), and releasing a claim is the outcome we want, not
    /// one to prevent. So a mid-flight suspension stops them taking anything *new* while still
    /// letting the current listing reach a clean end state.
    /// </para>
    /// <para>
    /// This also closes a pre-existing hole: nothing here checked AccountStatus at all, so admin
    /// Suspend previously only stopped a volunteer's push notifications — they could still claim,
    /// collect and deliver.
    /// </para>
    /// </summary>
    private async Task<string?> CheckCanTakeOnWorkAsync(CancellationToken cancellationToken)
    {
        var volunteer = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken);
        if (volunteer is null)
        {
            throw new NotFoundException("User", _currentUser.UserId);
        }

        return VerificationPolicy.CanTakeOnWork(volunteer.Role, volunteer.AccountStatus)
            ? null
            : VerificationPolicy.BlockedReason(volunteer.AccountStatus);
    }

    /// <summary>
    /// The volunteer's display name for a donor-facing notification body — a donor who is
    /// handing food to a stranger should be told who. Falls back to a neutral label rather
    /// than failing the whole operation if the lookup somehow comes back empty.
    /// </summary>
    private async Task<string> GetVolunteerNameAsync(Guid volunteerId, CancellationToken cancellationToken)
    {
        var volunteer = await _userRepository.GetByIdAsync(volunteerId, cancellationToken);
        return string.IsNullOrWhiteSpace(volunteer?.Name) ? "A volunteer" : volunteer!.Name;
    }

    /// <summary>
    /// Best-effort live push, always called *after* the owning transaction has committed —
    /// a SignalR failure (nobody connected, transient fault) must never roll back the status
    /// change it describes. The row is already persisted, so GET /api/notifications is the
    /// fallback for anyone who wasn't listening.
    /// </summary>
    private Task DispatchAsync(Notification notification, CancellationToken cancellationToken) =>
        _notificationDispatcher.DispatchAsync(notification, cancellationToken);

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
