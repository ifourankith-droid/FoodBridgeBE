using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Admin.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Admin;

public static class AdminMapper
{
    public static StatusCountResponse ToListingStatusResponse(this StatusCount statusCount) => new(((ListingStatus)statusCount.Status).ToString(), statusCount.Count);

    public static StatusCountResponse ToAccountStatusResponse(this StatusCount statusCount) => new(((AccountStatus)statusCount.Status).ToString(), statusCount.Count);

    public static AdminUserSummaryResponse ToResponse(this User user) => new(
        user.Id, user.Mobile, user.Name, user.Role.ToString(), user.AccountStatus.ToString(), user.City, user.IsAvailable, user.CreatedAtUtc);

    public static AdminUserSummaryResponse ToResponse(this AdminUserSummary summary) => new(
        summary.Id, summary.Mobile, summary.Name, summary.Role.ToString(), summary.AccountStatus.ToString(), summary.City, summary.IsAvailable, summary.CreatedAtUtc);

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
