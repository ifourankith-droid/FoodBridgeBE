using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

public interface IListingRepository
{
    /// <summary>
    /// Inserts the listing and its creation timeline event in one transaction.
    /// Mutates <paramref name="listing"/>.Id and <paramref name="creationEvent"/>.ListingId
    /// with the generated id.
    /// </summary>
    Task<Guid> CreateAsync(Listing listing, ListingTimelineEvent creationEvent, CancellationToken cancellationToken = default);

    Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetByDonorAsync(Guid donorId, ListingStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

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
    /// Atomically claims a Pending listing (Status = Pending → Claimed, VolunteerId set)
    /// and inserts the timeline event, in one conditional UPDATE + INSERT. Returns false
    /// if the listing was no longer Pending (already claimed, cancelled, etc.) — the
    /// caller distinguishes 404 (missing) from 409 (conflict) afterward.
    /// </summary>
    Task<bool> TryClaimAsync(Guid listingId, Guid volunteerId, ListingTimelineEvent claimEvent, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<NearbyListing> Items, int TotalCount)> GetNearbyPendingAsync(decimal latitude, decimal longitude, double radiusMeters, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Listings currently matched to this recipient and awaiting their accept/reject decision (Status = PickedUp).</summary>
    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetIncomingForRecipientAsync(Guid recipientId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>This recipient's past confirmed receipts (Status = Confirmed).</summary>
    Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetHistoryForRecipientAsync(Guid recipientId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Single-row timeline insert with no other side effects — used by accept, which doesn't change Status.</summary>
    Task AddTimelineEventAsync(ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default);

    /// <summary>Updates RecipientId (and UpdatedAtUtc) and inserts the timeline event atomically. Used by reject; Status is unchanged.</summary>
    Task ReassignRecipientAsync(Listing listing, ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically: Listings status → Confirmed, ListingTimeline insert, VolunteerPoints
    /// insert, Certificates insert (mutates <paramref name="certificate"/>.CertificateNumber
    /// with the generated number), and one Notifications insert per entry in
    /// <paramref name="notifications"/> — all in one transaction (all-or-nothing).
    /// </summary>
    Task ConfirmReceiptAsync(Listing listing, ListingTimelineEvent timelineEvent, VolunteerPoint volunteerPoint, Certificate certificate, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default);
}
