using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
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

    /// <summary>
    /// Fixed prefix for reject timeline notes — also used to recognize past rejections
    /// when building the reassignment exclude set. Keep the two in sync.
    /// </summary>
    private const string RejectedNotePrefix = "Recipient rejected the match.";

    private readonly IListingRepository _listingRepository;
    private readonly IRecipientMatcher _recipientMatcher;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public RecipientListingService(
        IListingRepository listingRepository,
        IRecipientMatcher recipientMatcher,
        INotificationDispatcher notificationDispatcher,
        ICurrentUser currentUser,
        IClock clock)
    {
        _listingRepository = listingRepository;
        _recipientMatcher = recipientMatcher;
        _notificationDispatcher = notificationDispatcher;
        _currentUser = currentUser;
        _clock = clock;
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

        await _listingRepository.ReassignRecipientAsync(listing, timelineEvent, cancellationToken);

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
        return listing.ToResponse(images, timeline);
    }
}
