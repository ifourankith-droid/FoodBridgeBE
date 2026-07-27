namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Read-only queries specific to the Donor/Volunteer/Recipient dashboard screens that
/// IReportsReader doesn't already cover (that one stays focused on the monthly/all-time
/// report shapes). Deliberately its own interface rather than growing IReportsReader —
/// "dashboard" and "report" are different consumers of overlapping but not identical data.
/// </summary>
public interface IDashboardReader
{
    /// <summary>Sum of QuantityMeals across this donor's Confirmed listings whose confirm time (UpdatedAtUtc) falls on nowUtc's calendar date.</summary>
    Task<int> GetDonorMealsDonatedTodayAsync(Guid donorId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Nearest Verified, non-deleted recipients to the point — informational browsing for a donor, not a match, so not filtered by IsAvailable.</summary>
    Task<IReadOnlyList<NearbyRecipient>> GetNearbyRecipientsAsync(decimal latitude, decimal longitude, double radiusMeters, int limit, CancellationToken cancellationToken = default);

    /// <summary>Sum of QuantityMeals across this volunteer's Confirmed deliveries — independent of VolunteerPoints' point formula, so it stays correct if that formula ever changes.</summary>
    Task<int> GetVolunteerMealsHelpedAsync(Guid volunteerId, CancellationToken cancellationToken = default);

    /// <summary>Sum of QuantityMeals across this recipient's Confirmed listings whose confirm time (UpdatedAtUtc) falls on nowUtc's calendar date.</summary>
    Task<int> GetRecipientMealsReceivedTodayAsync(Guid recipientId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Per-donor breakdown of this recipient's Confirmed meals received, highest first.</summary>
    Task<IReadOnlyList<DonorMealShare>> GetRecipientDonorDistributionAsync(Guid recipientId, int limit, CancellationToken cancellationToken = default);
}
