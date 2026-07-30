using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Admin.Dtos;
using FoodBridge.Application.Users;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Admin;

public static class AdminMapper
{
    public static StatusCountResponse ToListingStatusResponse(this StatusCount statusCount) => new(((ListingStatus)statusCount.Status).ToString(), statusCount.Count);

    public static StatusCountResponse ToAccountStatusResponse(this StatusCount statusCount) => new(((AccountStatus)statusCount.Status).ToString(), statusCount.Count);

    public static AdminUserSummaryResponse ToResponse(this User user) => Build(
        user.Id, user.Mobile, user.Name, user.Role, user.AccountStatus, user.City, user.IsAvailable, user.CreatedAtUtc,
        Array.Empty<UserDocumentType>());

    public static AdminUserSummaryResponse ToResponse(this AdminUserSummary summary) => Build(
        summary.Id, summary.Mobile, summary.Name, summary.Role, summary.AccountStatus, summary.City, summary.IsAvailable, summary.CreatedAtUtc,
        summary.SubmittedDocumentTypes);

    /// <summary>
    /// Shared projection so both overloads derive `requiredDocumentTypes`/`isReadyForReview` from
    /// <see cref="VerificationPolicy"/> identically — the single-account response after a
    /// verify/suspend must agree with the same row in the browse list.
    /// </summary>
    private static AdminUserSummaryResponse Build(
        Guid id,
        string mobile,
        string name,
        UserRole role,
        AccountStatus accountStatus,
        string? city,
        bool isAvailable,
        DateTime createdAtUtc,
        IReadOnlyList<UserDocumentType> submitted)
    {
        var required = VerificationPolicy.RequiredDocuments(role);
        var isReadyForReview = accountStatus == AccountStatus.Pending
            && required.Count > 0
            && required.All(submitted.Contains);

        return new AdminUserSummaryResponse(
            id, mobile, name, role.ToString(), accountStatus.ToString(), city, isAvailable, createdAtUtc,
            required.Select(t => t.ToString()).ToList(),
            submitted.Select(t => t.ToString()).ToList(),
            isReadyForReview);
    }

    public static AdminListingSummaryResponse ToResponse(this AdminListingSummary summary) => new(
        summary.Id, summary.Title, summary.Status.ToString(), summary.DonorId, summary.DonorName,
        summary.VolunteerId, summary.RecipientId, summary.QuantityMeals, summary.PickupDeadlineUtc, summary.CreatedAtUtc);

    public static AdminDashboardResponse ToResponse(this AdminDashboardStats stats, IReadOnlyList<StatusCount> listingsByStatus, IReadOnlyList<StatusCount> accountsByStatus) => new(
        stats.TotalDonors, stats.TotalVolunteers, stats.TotalRecipients, stats.PendingRecipients,
        stats.TotalListings, stats.PendingListings, stats.ActiveListings, stats.ConfirmedListings,
        stats.TotalMealsDonated, stats.TotalCertificatesIssued, stats.TotalVolunteerPointsAwarded,
        stats.OpenDisputes, stats.ResolvedDisputes,
        listingsByStatus.Select(x => x.ToListingStatusResponse()).ToList(),
        accountsByStatus.Select(x => x.ToAccountStatusResponse()).ToList());
}
