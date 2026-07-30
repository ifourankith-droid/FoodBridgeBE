using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Outcome of one <see cref="IListingRepository.ExpirePastDeadlineListingsAsync"/> sweep.
/// </summary>
/// <param name="ExpiredIds">Listings flipped Pending → Expired (deadline passed, never collected).</param>
/// <param name="RevertedToPendingIds">
/// Listings reverted Claimed → Pending because the volunteer never collected. Always a subset of
/// the rows that then expire in the same sweep, since a reverted listing's deadline has by
/// definition already passed.
/// </param>
/// <param name="Notifications">
/// Rows already persisted inside the sweep's transaction — the affected donors and volunteers.
/// Returned so the caller can push them live *after* the commit, the same
/// write-then-dispatch ordering every other notification in this app uses.
/// </param>
public sealed record ExpirySweepResult(
    IReadOnlyList<Guid> ExpiredIds,
    IReadOnlyList<Guid> RevertedToPendingIds,
    IReadOnlyList<Notification> Notifications);
