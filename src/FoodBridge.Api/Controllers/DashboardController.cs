using FoodBridge.Application.Common;
using FoodBridge.Application.Dashboard;
using FoodBridge.Application.Dashboard.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// One consolidated, chart-ready dashboard endpoint per role — everything the prototype's
/// role-specific dashboard screen needs in a single call, instead of composing it client-side
/// from reports/leaderboard/listings. Each action carries its own role policy instead of a
/// shared class-level one, same reasoning as ReportsController.
/// </summary>
[Route("api/dashboard")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class DashboardController : BaseController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>latitude/longitude are optional — omit to use the donor's own registered profile location for "nearby recipients."</summary>
    [Authorize(Policy = "DonorOnly")]
    [HttpGet("donor")]
    [ProducesResponseType(typeof(ApiResponse<DonorDashboardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<DonorDashboardResponse>>> GetDonorDashboard(
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetDonorDashboardAsync(latitude, longitude, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>latitude/longitude are optional — omit to use the volunteer's own registered profile location for "open listings nearby."</summary>
    [Authorize(Policy = "VolunteerOnly")]
    [HttpGet("volunteer")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerDashboardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VolunteerDashboardResponse>>> GetVolunteerDashboard(
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetVolunteerDashboardAsync(latitude, longitude, cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "RecipientOnly")]
    [HttpGet("recipient")]
    [ProducesResponseType(typeof(ApiResponse<RecipientDashboardResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RecipientDashboardResponse>>> GetRecipientDashboard(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetRecipientDashboardAsync(cancellationToken);
        return HandleResult(result);
    }
}
