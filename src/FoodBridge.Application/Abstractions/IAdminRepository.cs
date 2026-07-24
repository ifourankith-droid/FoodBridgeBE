using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Cross-aggregate admin-visibility queries (Users + Listings + Certificates +
/// VolunteerPoints + Disputes) — deliberately separate from IUserRepository/
/// IListingRepository, since "give admin a bird's-eye view" is a distinct read
/// concern from either aggregate's own CRUD (same ISP reasoning as ILeaderboardReader/
/// IReportsReader in Phase 8).
/// </summary>
public interface IAdminRepository
{
    Task<AdminDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminListingSummary> Items, int TotalCount)> GetAllListingsAsync(ListingStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminUserSummary> Items, int TotalCount)> GetAllUsersAsync(UserRole? role, AccountStatus? accountStatus, int page, int pageSize, CancellationToken cancellationToken = default);
}
