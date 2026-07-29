using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

public interface IListingRepository
{
    /// <summary>
    /// Inserts the listing, its creation timeline event, and one Notifications row per
    /// entry in <paramref name="volunteerNotifications"/> (the nearby volunteers to alert)
    /// — all in one transaction. Mutates <paramref name="listing"/>.Id and
    /// <paramref name="creationEvent"/>.ListingId with the generated id.
    /// </summary>
    Task<Guid> CreateAsync(Listing listing, ListingTimelineEvent creationEvent, IReadOnlyList<Notification> volunteerNotifications, CancellationToken cancellationToken = default);

    Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetByDonorAsync(Guid donorId, ListingStatus? status, DietType? dietType, MealType? mealType, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingImage>> GetImagesAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingTimelineEvent>> GetTimelineAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Listing listing, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the listing's status and inserts the corresponding timeline event
    /// in one transaction.
    /// </summary>
    Task ChangeStatusAsync(Listing listing, ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default);

    Task<Guid> AddImageAsync(ListingImage image, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a Pending listing (Status = Pending → Claimed, VolunteerId and
    /// EstimatedPickupAtUtc set) and inserts the timeline event, in one conditional
    /// UPDATE + INSERT. Returns false if the listing was no longer Pending (already
    /// claimed, cancelled, etc.) — the caller distinguishes 404 (missing) from 409
    /// (conflict) afterward.
    /// </summary>
    Task<bool> TryClaimAsync(Guid listingId, Guid volunteerId, DateTime? estimatedPickupAtUtc, ListingTimelineEvent claimEvent, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<NearbyListing> Items, int TotalCount)> GetNearbyPendingAsync(decimal latitude, decimal longitude, double radiusMeters, DietType? dietType, MealType? mealType, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Donations a recipient could still receive — Pending or Claimed, deadline not passed,
    /// within <paramref name="radiusMeters"/> — that are either unspoken-for or already
    /// requested by this same recipient. Ordered by ascending distance.
    /// </summary>
    Task<(IReadOnlyList<AvailableNearbyListing> Items, int TotalCount)> GetAvailableNearbyForRecipientAsync(Guid recipientId, decimal latitude, decimal longitude, double radiusMeters, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conditionally reserves an as-yet-unmatched listing for a recipient and records the
    /// timeline event, atomically. Status is unchanged — this only pre-sets RecipientId so
    /// the volunteer's confirm-pickup keeps it instead of running the nearest-available
    /// matcher. False when another recipient got there first or the listing moved on;
    /// the caller distinguishes 404 from 409 afterward. When the listing already has a
    /// volunteer, <paramref name="volunteerNotification"/> tells them where it is now
    /// headed, inserted in the same transaction so it can't outlive a failed reservation.
    /// </summary>
    Task<bool> TryRequestForRecipientAsync(Guid listingId, Guid recipientId, ListingTimelineEvent requestEvent, Notification? volunteerNotification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a request this recipient made, provided the food hasn't been collected yet.
    /// False when the listing is no longer theirs to release (reassigned, or already PickedUp
    /// — past that point the recipient must accept/reject instead).
    /// </summary>
    Task<bool> TryWithdrawRecipientRequestAsync(Guid listingId, Guid recipientId, ListingTimelineEvent withdrawEvent, CancellationToken cancellationToken = default);

    /// <summary>Listings currently matched to this recipient and awaiting their accept/reject decision (Status = PickedUp).</summary>
    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetIncomingForRecipientAsync(Guid recipientId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>This recipient's past confirmed receipts (Status = Confirmed).</summary>
    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetHistoryForRecipientAsync(Guid recipientId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Single-row timeline insert with no other side effects — used by accept, which doesn't change Status.</summary>
    Task AddTimelineEventAsync(ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates RecipientId (and UpdatedAtUtc) and inserts the timeline event atomically.
    /// Used by reject; Status is unchanged. When <paramref name="volunteerNotification"/>
    /// is non-null (every recipient has now been exhausted), also inserts it in the same
    /// transaction — the volunteer, not the rejecting recipient, needs to hear about the
    /// suggested fallback drop-off location, so it travels as a notification rather than
    /// in this call's own (recipient-facing) response.
    /// </summary>
    Task ReassignRecipientAsync(Listing listing, ListingTimelineEvent timelineEvent, Notification? volunteerNotification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically: Listings status → Confirmed, ListingTimeline insert, VolunteerPoints
    /// insert, Certificates insert (mutates <paramref name="certificate"/>.CertificateNumber
    /// with the generated number), and one Notifications insert per entry in
    /// <paramref name="notifications"/> — all in one transaction (all-or-nothing).
    /// </summary>
    Task ConfirmReceiptAsync(Listing listing, ListingTimelineEvent timelineEvent, VolunteerPoint volunteerPoint, Certificate certificate, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically, in one sweep: (1) reverts every Claimed listing whose PickupDeadlineUtc
    /// has passed back to Pending (clearing VolunteerId) — reuses the state machine's
    /// existing, already-legal Claimed→Pending transition, not a new one — so a volunteer
    /// who claimed and never showed up doesn't leave perishable food stuck forever; then
    /// (2) expires every Pending listing whose deadline has passed, including rows just
    /// reverted in step 1. Inserts a system timeline event (ActorUserId null) for each
    /// change. Returns the expired and reverted-to-pending ids separately, for logging.
    /// </summary>
    Task<(IReadOnlyList<Guid> ExpiredIds, IReadOnlyList<Guid> RevertedToPendingIds)> ExpirePastDeadlineListingsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
