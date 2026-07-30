using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Listings;

/// <summary>
/// The five-part atomic payload <see cref="Abstractions.IListingRepository.ConfirmReceiptAsync"/>
/// writes when a donation completes.
/// </summary>
public sealed record ListingCompletionPayload(
    ListingTimelineEvent TimelineEvent,
    VolunteerPoint VolunteerPoint,
    Certificate Certificate,
    IReadOnlyList<Notification> Notifications,
    int Points);

/// <summary>
/// Builds the completion payload for the two paths that can finish a donation: the
/// recipient's confirm-receipt, and — when no recipient was ever matched, i.e. the
/// Recipient role is disabled — the volunteer's confirm-delivery. Both award the same
/// points, issue the same certificate, and notify the same parties, so the formula and
/// the notification shapes live here once rather than being duplicated per service.
/// </summary>
public static class ListingCompletion
{
    /// <summary>
    /// Points awarded per meal delivered. No formula is specified by the product, so this
    /// is a plain, explicit assumption tied to the platform's "meals rescued" framing —
    /// change it here and both completion paths follow.
    /// </summary>
    public const int PointsPerMeal = 1;

    /// <summary>
    /// Assembles the timeline event, volunteer points ledger row, donor certificate, and
    /// the donor/volunteer notifications for a listing about to move to
    /// <see cref="ListingStatus.Confirmed"/>. Does not mutate <paramref name="listing"/> —
    /// the caller sets Status/UpdatedAtUtc so the repository writes them in the same
    /// transaction as this payload.
    /// </summary>
    /// <param name="listing">The listing being completed. Must have a VolunteerId.</param>
    /// <param name="actorUserId">Who confirmed — the recipient, or the volunteer when no recipient was matched.</param>
    /// <param name="note">Timeline note recording which path completed the donation.</param>
    /// <param name="photoUrl">Proof-of-delivery photo, when the completing action carries one.</param>
    /// <param name="nowUtc">Single timestamp shared by every row, from IClock.</param>
    public static ListingCompletionPayload Build(
        Listing listing,
        Guid actorUserId,
        string note,
        string? photoUrl,
        DateTime nowUtc)
    {
        if (listing.VolunteerId is null)
        {
            throw new InvalidOperationException("A listing cannot be completed without an assigned volunteer.");
        }

        var volunteerId = listing.VolunteerId.Value;
        var points = listing.QuantityMeals * PointsPerMeal;

        var timelineEvent = new ListingTimelineEvent
        {
            ListingId = listing.Id,
            FromStatus = listing.Status,
            ToStatus = ListingStatus.Confirmed,
            ActorUserId = actorUserId,
            Note = note,
            PhotoUrl = photoUrl,
            CreatedAtUtc = nowUtc,
        };

        var volunteerPoint = new VolunteerPoint
        {
            VolunteerId = volunteerId,
            ListingId = listing.Id,
            Points = points,
            Reason = $"Delivered '{listing.Title}' ({listing.QuantityMeals} meals).",
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

        var certificate = new Certificate
        {
            DonorId = listing.DonorId,
            ListingId = listing.Id,
            MealsCount = listing.QuantityMeals,
            IssuedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
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
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            },
            new()
            {
                UserId = volunteerId,
                Type = "PointsAwarded",
                Title = "Points awarded",
                Body = $"You earned {points} points for delivering '{listing.Title}'.",
                IsRead = false,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            },
        };

        return new ListingCompletionPayload(timelineEvent, volunteerPoint, certificate, notifications, points);
    }
}
