using FoodBridge.Application.Common;
using FoodBridge.Application.Reports;
using FoodBridge.Application.Reports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Chart-ready impact reports, one per role. Each action carries its own role policy
/// instead of a shared class-level one, since every action needs a different role.
/// </summary>
[Route("api/reports")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class ReportsController : BaseController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [Authorize(Policy = "DonorOnly")]
    [HttpGet("donor")]
    [ProducesResponseType(typeof(ApiResponse<DonorReportResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DonorReportResponse>>> GetDonorReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetDonorReportAsync(cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "VolunteerOnly")]
    [HttpGet("volunteer")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerReportResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VolunteerReportResponse>>> GetVolunteerReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetVolunteerReportAsync(cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "RecipientOnly")]
    [HttpGet("recipient")]
    [ProducesResponseType(typeof(ApiResponse<RecipientReportResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RecipientReportResponse>>> GetRecipientReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetRecipientReportAsync(cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("platform")]
    [ProducesResponseType(typeof(ApiResponse<PlatformReportResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PlatformReportResponse>>> GetPlatformReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetPlatformReportAsync(cancellationToken);
        return HandleResult(result);
    }
}
