using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Listings;

/// <summary>
/// Factory for the notifications a listing's lifecycle produces, one method per event.
/// Centralised for the same reason as <see cref="ListingCompletion"/>: the wording and the
/// <c>Type</c> strings are a contract with the frontend's per-type icon/colour/destination
/// registry (<c>core/models/notification.model.ts</c>), so they must not be re-typed inline
/// at each call site.
/// <para>
/// <b>Every type here is dispatched to exactly one role.</b> The frontend maps a type to a
/// single destination page and re-checks it against the signed-in role, so a type sent to
/// two different roles would be unroutable for one of them. Where both parties need telling
/// about the same underlying event, that's two distinct types
/// (<c>ListingClaimReverted</c> → donor, <c>ClaimExpired</c> → volunteer).
/// </para>
/// <para>
/// <c>PayloadJson</c> is deliberately left null throughout, matching every pre-existing
/// notification: the frontend routes to the relevant *page*, not to a specific record, so
/// there is nothing for a payload to carry yet.
/// </para>
/// </summary>
public static class ListingNotifications
{
    /// <summary>To the donor: a volunteer has committed to collecting their donation.</summary>
    public static Notification Claimed(Listing listing, string volunteerName, DateTime? estimatedPickupAtUtc, DateTime nowUtc) =>
        For(
            listing.DonorId,
            "ListingClaimed",
            "Your donation was claimed",
            estimatedPickupAtUtc.HasValue
                ? $"{volunteerName} will collect '{listing.Title}' — estimated pickup {estimatedPickupAtUtc.Value:dd MMM, HH:mm} UTC."
                : $"{volunteerName} will collect '{listing.Title}'.",
            nowUtc);

    /// <summary>To the donor: the food has physically left their premises.</summary>
    public static Notification PickedUp(Listing listing, string volunteerName, DateTime nowUtc) =>
        For(
            listing.DonorId,
            "ListingPickedUp",
            "Your donation was collected",
            $"{volunteerName} has collected '{listing.Title}' ({listing.QuantityMeals} meals) and is on the way.",
            nowUtc);

    /// <summary>
    /// To the donor: the volunteer handed the claim back, so the listing is open again and
    /// needs someone new. Worth telling them — their food is perishable and the clock is running.
    /// </summary>
    public static Notification Unclaimed(Listing listing, DateTime nowUtc) =>
        For(
            listing.DonorId,
            "ListingUnclaimed",
            "Your donation is open again",
            $"The volunteer released their claim on '{listing.Title}'. It's back in the open feed for another volunteer.",
            nowUtc);

    // There is intentionally no "donor cancelled your pickup" notification. `CancelAsync`
    // only permits Pending → Cancelled (see ListingStateMachine), and a Pending listing never
    // has a VolunteerId — both `unclaim` and the expiry sweep's revert null the column — so a
    // cancel can never strand an assigned volunteer. Such a notification would be dead code.
    // If `Claimed → Cancelled` is ever added to the state machine, add the notification with it.

    /// <summary>
    /// To the donor: half the pickup window has gone and no volunteer has claimed it. Sent once, and
    /// framed around the action available to them — they can still deliver it themselves rather than
    /// watch perishable food run out the clock.
    /// </summary>
    public static Notification HalfwayUnclaimed(Guid donorId, string listingTitle, DateTime deadlineUtc, DateTime nowUtc)
    {
        var remaining = deadlineUtc - nowUtc;
        // Hours read better than "0.7 hours" once it's under a couple of hours.
        var remainingText = remaining.TotalHours >= 2
            ? $"about {(int)Math.Round(remaining.TotalHours)} hours"
            : $"about {Math.Max(1, (int)Math.Round(remaining.TotalMinutes))} minutes";

        return For(
            donorId,
            "ListingHalfwayUnclaimed",
            "Still waiting for a volunteer",
            $"No one has claimed '{listingTitle}' yet, and {remainingText} of your pickup window is left. "
            + "You can wait, or deliver it yourself to a nearby drop-off point.",
            nowUtc);
    }

    /// <summary>To the donor: nobody claimed it before the deadline.</summary>
    public static Notification Expired(Guid donorId, string listingTitle, DateTime nowUtc) =>
        For(
            donorId,
            "ListingExpired",
            "Your donation expired unclaimed",
            $"'{listingTitle}' passed its pickup deadline without being collected. Please handle the food safely.",
            nowUtc);

    /// <summary>
    /// To the volunteer: their claim lapsed because they didn't collect in time.
    /// <para>
    /// There is deliberately no donor-facing counterpart for the revert. The sweep reverts
    /// <c>Claimed AND deadline &lt;= now</c> to Pending, then expires <c>Pending AND deadline
    /// &lt;= now</c> — so a reverted listing is *always* expired in the very same sweep, and
    /// the donor already gets <see cref="Expired"/>. A separate "it's open again" message
    /// would be immediately contradicted by the expiry notice arriving beside it.
    /// </para>
    /// </summary>
    public static Notification ClaimExpired(Guid volunteerId, string listingTitle, DateTime nowUtc) =>
        For(
            volunteerId,
            "ClaimExpired",
            "Your claim expired",
            $"You didn't collect '{listingTitle}' before its pickup deadline, so the claim was released.",
            nowUtc);

    private static Notification For(Guid userId, string type, string title, string body, DateTime nowUtc) =>
        new()
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            IsRead = false,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
}
