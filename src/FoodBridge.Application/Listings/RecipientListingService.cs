using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations;
using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;
using FoodBridge.Domain.StateMachines;

namespace FoodBridge.Application.Listings;

public sealed class RecipientListingService : IRecipientListingService
{
    /// <summary>Simple, explicit assumption — 1 point per meal delivered — since no point formula is specified.</summary>
    private const int PointsPerMeal = 1;

    /// <summary>Browse radius for the available-nearby feed. Same bounds as the volunteer's.</summary>
    private const double DefaultRadiusKm = 10;
    private const double MaxRadiusKm = 50;

    /// <summary>
    /// Fixed prefix for reject timeline notes — also used to recognize past rejections
    /// when building the reassignment exclude set. Keep the two in sync.
    /// </summary>
    private const string RejectedNotePrefix = "Recipient rejected the match.";

    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRecipientMatcher _recipientMatcher;
    private readonly IDropOffLocationRepository _dropOffLocationRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public RecipientListingService(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        IRecipientMatcher recipientMatcher,
        IDropOffLocationRepository dropOffLocationRepository,
        INotificationDispatcher notificationDispatcher,
        ICurrentUser currentUser,
        IClock clock)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _recipientMatcher = recipientMatcher;
        _dropOffLocationRepository = dropOffLocationRepository;
        _notificationDispatcher = notificationDispatcher;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PagedResult<ListingAvailableNearbyResponse>>> GetAvailableNearbyAsync(decimal latitude, decimal longitude, double? radiusKm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (latitude < -90 || latitude > 90)
        {
            return Result.Failure<PagedResult<ListingAvailableNearbyResponse>>("Latitude must be between -90 and 90.");
        }

        if (longitude < -180 || longitude > 180)
        {
            return Result.Failure<PagedResult<ListingAvailableNearbyResponse>>("Longitude must be between -180 and 180.");
        }

        var effectiveRadiusKm = radiusKm switch
        {
            null or <= 0 => DefaultRadiusKm,
            > MaxRadiusKm => MaxRadiusKm,
            _ => radiusKm.Value,
        };

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetAvailableNearbyForRecipientAsync(
            _currentUser.UserId, latitude, longitude, effectiveRadiusKm * 1000, normalizedPage, normalizedPageSize, cancellationToken);

        var responses = items.Select(i => i.ToResponse()).ToList();
        return Result.Success(new PagedResult<ListingAvailableNearbyResponse>(responses, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<ListingResponse>> RequestAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        // Same bar the automatic matcher applies — an unverified organization can't be
        // routed a donation, so it mustn't be able to reserve one either.
        var caller = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken);
        if (caller is null || caller.AccountStatus != AccountStatus.Verified)
        {
            return Result.Failure<ListingResponse>("Your organization must be verified before you can request donations.");
        }

        if (listing.RecipientId == _currentUser.UserId)
        {
            // Idempotent: re-requesting what you already hold is a no-op, not an error.
            return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Donation already requested.");
        }

        if (listing.RecipientId is not null)
        {
            throw new ConflictException("This donation has already been matched to another organization.");
        }

        if (listing.Status is not (ListingStatus.Pending or ListingStatus.Claimed))
        {
            throw new BusinessRuleException($"Only a donation that hasn't been collected yet can be requested (current status: {listing.Status}).");
        }

        var now = _clock.UtcNow;
        if (listing.PickupDeadlineUtc <= now)
        {
            throw new BusinessRuleException("This donation's pickup window has already closed.");
        }

        // Status is deliberately unchanged — requesting reserves the destination, it does
        // not move the listing through the pickup/delivery state machine.
        var requestEvent = new ListingTimelineEvent
        {
            FromStatus = listing.Status,
            ToStatus = listing.Status,
            ActorUserId = _currentUser.UserId,
            Note = $"Requested by recipient {caller.Name}.",
            CreatedAtUtc = now,
        };

        // A volunteer already carrying this listing would otherwise only learn its
        // destination at pickup time — tell them now so they can plan the drop-off.
        // Written inside the same transaction as the reservation, so a lost race can't
        // leave behind a notification for a delivery that isn't happening.
        var volunteerNotification = listing.VolunteerId is null
            ? null
            : new Notification
            {
                UserId = listing.VolunteerId.Value,
                Type = "RecipientRequested",
                Title = "Drop-off confirmed",
                Body = $"'{listing.Title}' is going to {caller.Name}. Deliver it there once you've collected it.",
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

        var reserved = await _listingRepository.TryRequestForRecipientAsync(listingId, _currentUser.UserId, requestEvent, volunteerNotification, cancellationToken);
        if (!reserved)
        {
            throw new ConflictException("This donation is no longer available to request.");
        }

        listing.RecipientId = _currentUser.UserId;
        listing.UpdatedAtUtc = now;

        // Best-effort live push, after the atomic write has already committed — same
        // reasoning as ConfirmReceiptAsync.
        if (volunteerNotification is not null)
        {
            await _notificationDispatcher.DispatchAsync(volunteerNotification, cancellationToken);
        }

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Donation requested successfully.");
    }

    public async Task<Result<ListingResponse>> WithdrawRequestAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        if (listing.RecipientId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only withdraw a request you made.");
        }

        if (listing.Status is not (ListingStatus.Pending or ListingStatus.Claimed))
        {
            throw new BusinessRuleException("The food has already been collected — accept or reject it instead.");
        }

        var now = _clock.UtcNow;
        var withdrawEvent = new ListingTimelineEvent
        {
            FromStatus = listing.Status,
            ToStatus = listing.Status,
            ActorUserId = _currentUser.UserId,
            Note = "Recipient withdrew their request.",
            CreatedAtUtc = now,
        };

        var released = await _listingRepository.TryWithdrawRecipientRequestAsync(listingId, _currentUser.UserId, withdrawEvent, cancellationToken);
        if (!released)
        {
            throw new ConflictException("This request can no longer be withdrawn.");
        }

        listing.RecipientId = null;
        listing.UpdatedAtUtc = now;

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Request withdrawn successfully.");
    }

    public async Task<Result<PagedResult<ListingSummaryResponse>>> GetIncomingAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetIncomingForRecipientAsync(_currentUser.UserId, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<ListingSummaryResponse>(items.Select(l => l.ToSummaryResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<ListingResponse>> AcceptAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetMatchedListingOrThrowAsync(listingId, cancellationToken);
        EnsureAwaitingDecision(listing);

        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = listing.Status,
            ActorUserId = _currentUser.UserId,
            Note = "Recipient accepted the match.",
            CreatedAtUtc = _clock.UtcNow,
        };

        await _listingRepository.AddTimelineEventAsync(timelineEvent, cancellationToken);

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Match accepted successfully.");
    }

    public async Task<Result<ListingResponse>> RejectAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetMatchedListingOrThrowAsync(listingId, cancellationToken);
        EnsureAwaitingDecision(listing);

        var excludeRecipientIds = await GetPreviouslyRejectedByAsync(listing, cancellationToken);
        var newRecipientId = await _recipientMatcher.FindNearestAvailableRecipientAsync(listing.Latitude, listing.Longitude, excludeRecipientIds, cancellationToken);

        var now = _clock.UtcNow;
        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = listing.Status,
            ActorUserId = _currentUser.UserId,
            Note = newRecipientId is null
                ? $"{RejectedNotePrefix} No other recipient is currently available."
                : $"{RejectedNotePrefix} Reassigned to another available recipient.",
            CreatedAtUtc = now,
        };

        listing.RecipientId = newRecipientId;
        listing.UpdatedAtUtc = now;

        // Every recipient is now exhausted — the volunteer (who isn't the caller here, so
        // can't just read it off this response) needs to be told where to take the food
        // instead. Resolved before the write so the notification body already has it.
        Notification? volunteerNotification = null;
        if (newRecipientId is null)
        {
            var nearestDropOff = await _dropOffLocationRepository.GetNearestActiveAsync(listing.Latitude, listing.Longitude, cancellationToken);
            volunteerNotification = new Notification
            {
                UserId = listing.VolunteerId!.Value,
                Type = "DropOffLocationSuggested",
                Title = "No recipient available",
                Body = nearestDropOff is null
                    ? $"No recipient is currently available for '{listing.Title}', and no fallback drop-off location is configured yet. Please use your judgement."
                    : $"No recipient is currently available for '{listing.Title}'. Please take it to {nearestDropOff.Name}, {nearestDropOff.Address}.",
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }

        await _listingRepository.ReassignRecipientAsync(listing, timelineEvent, volunteerNotification, cancellationToken);

        // Best-effort live push, after the atomic write has already committed — same
        // reasoning as ConfirmReceiptAsync below.
        if (volunteerNotification is not null)
        {
            await _notificationDispatcher.DispatchAsync(volunteerNotification, cancellationToken);
        }

        return Result.Success(await BuildResponseAsync(listing, cancellationToken), "Match rejected; reassigned automatically if another recipient was available.");
    }

    public async Task<Result<ConfirmReceiptResponse>> ConfirmReceiptAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await GetMatchedListingOrThrowAsync(listingId, cancellationToken);
        ListingStateMachine.EnsureCanTransition(listing.Status, ListingStatus.Confirmed);

        var now = _clock.UtcNow;
        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = ListingStatus.Confirmed,
            ActorUserId = _currentUser.UserId,
            Note = "Receipt confirmed by recipient.",
            CreatedAtUtc = now,
        };

        var points = listing.QuantityMeals * PointsPerMeal;
        var volunteerPoint = new VolunteerPoint
        {
            VolunteerId = listing.VolunteerId!.Value,
            ListingId = listing.Id,
            Points = points,
            Reason = $"Delivered '{listing.Title}' ({listing.QuantityMeals} meals).",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var certificate = new Certificate
        {
            DonorId = listing.DonorId,
            ListingId = listing.Id,
            MealsCount = listing.QuantityMeals,
            IssuedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var notifications = new List<Notification>
        {
            new()
            {
                UserId = listing.DonorId,
                Type = "DonationConfirmed",
                Title = "Donation confirmed",
                Body = $"Your donation '{listing.Title}' was received and confirmed. A certificate has been issued.",
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            new()
            {
                UserId = listing.VolunteerId!.Value,
                Type = "PointsAwarded",
                Title = "Points awarded",
                Body = $"You earned {points} points for delivering '{listing.Title}'.",
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
        };

        listing.Status = ListingStatus.Confirmed;
        listing.UpdatedAtUtc = now;

        await _listingRepository.ConfirmReceiptAsync(listing, timelineEvent, volunteerPoint, certificate, notifications, cancellationToken);

        // Best-effort live push, after the atomic write has already committed — a
        // dispatch failure (e.g. nobody connected) must never roll back the receipt.
        foreach (var notification in notifications)
        {
            await _notificationDispatcher.DispatchAsync(notification, cancellationToken);
        }

        var response = new ConfirmReceiptResponse(await BuildResponseAsync(listing, cancellationToken), certificate.CertificateNumber, points);
        return Result.Success(response, "Receipt confirmed successfully.");
    }

    public async Task<Result<PagedResult<ListingSummaryResponse>>> GetHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _listingRepository.GetHistoryForRecipientAsync(_currentUser.UserId, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<ListingSummaryResponse>(items.Select(l => l.ToSummaryResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>
    /// Every recipient who has already rejected this listing, plus the current one —
    /// without this, with only two available recipients in the system, rejects would
    /// just ping-pong between them forever instead of ever reaching "no recipient
    /// available". Derived from the timeline's reject notes rather than a new table,
    /// matching the "simple auto-reassignment" scope.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>> GetPreviouslyRejectedByAsync(Listing listing, CancellationToken cancellationToken)
    {
        var timeline = await _listingRepository.GetTimelineAsync(listing.Id, cancellationToken);
        var excluded = timeline
            .Where(t => t.ActorUserId.HasValue && t.Note is not null && t.Note.StartsWith(RejectedNotePrefix, StringComparison.Ordinal))
            .Select(t => t.ActorUserId!.Value)
            .ToHashSet();
        excluded.Add(listing.RecipientId!.Value);
        return excluded;
    }

    private static void EnsureAwaitingDecision(Listing listing)
    {
        if (listing.Status != ListingStatus.PickedUp)
        {
            throw new BusinessRuleException("Only an in-transit listing awaiting your decision can be accepted or rejected.");
        }
    }

    private async Task<Listing> GetMatchedListingOrThrowAsync(Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            throw new NotFoundException("Listing", listingId);
        }

        if (listing.RecipientId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only act on listings matched to you.");
        }

        return listing;
    }

    private async Task<ListingResponse> BuildResponseAsync(Listing listing, CancellationToken cancellationToken)
    {
        var images = await _listingRepository.GetImagesAsync(listing.Id, cancellationToken);
        var timeline = await _listingRepository.GetTimelineAsync(listing.Id, cancellationToken);
        return await listing.ToResponseAsync(images, timeline, _userRepository, cancellationToken);
    }
}
