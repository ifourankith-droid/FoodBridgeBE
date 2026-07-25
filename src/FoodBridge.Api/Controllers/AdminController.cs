using FoodBridge.Application.Admin;
using FoodBridge.Application.Admin.Dtos;
using FoodBridge.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Admin dashboard, and listings/accounts browsing and moderation.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class AdminController : BaseController
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminDashboardResponse>>> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetDashboardAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>All listings platform-wide, optionally filtered by status.</summary>
    [HttpGet("listings")]
    [ProducesResponseType(typeof(PagedResponse<AdminListingSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminListingSummaryResponse>>> GetAllListings(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetAllListingsAsync(status, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>All user accounts platform-wide, optionally filtered by role and/or account status.</summary>
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(PagedResponse<AdminUserSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminUserSummaryResponse>>> GetAllAccounts(
        [FromQuery] string? role,
        [FromQuery] string? accountStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetAllUsersAsync(role, accountStatus, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>Sets AccountStatus to Verified — e.g. unlocks a Pending recipient for RecipientMatcher.</summary>
    [HttpPatch("accounts/{id:guid}/verify")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AdminUserSummaryResponse>>> VerifyAccount(Guid id, CancellationToken cancellationToken)
    {
        var result = await _adminService.VerifyAccountAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Sets AccountStatus to Suspended. Refuses to suspend Admin accounts or the caller's own account.</summary>
    [HttpPatch("accounts/{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AdminUserSummaryResponse>>> SuspendAccount(Guid id, CancellationToken cancellationToken)
    {
        var result = await _adminService.SuspendAccountAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
