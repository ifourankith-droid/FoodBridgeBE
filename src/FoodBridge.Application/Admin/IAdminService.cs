using FoodBridge.Application.Admin.Dtos;
using FoodBridge.Application.Common;

namespace FoodBridge.Application.Admin;

public interface IAdminService
{
    Task<Result<AdminDashboardResponse>> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<Result<PagedResult<AdminListingSummaryResponse>>> GetAllListingsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<AdminUserSummaryResponse>>> GetAllUsersAsync(string? role, string? accountStatus, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Sets AccountStatus to Verified — e.g. unlocks a Pending recipient for RecipientMatcher.</summary>
    Task<Result<AdminUserSummaryResponse>> VerifyAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sets AccountStatus to Suspended. Refuses to suspend Admin accounts or the caller's own account.</summary>
    Task<Result<AdminUserSummaryResponse>> SuspendAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
