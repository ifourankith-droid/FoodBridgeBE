using FoodBridge.Application.Common;
using FoodBridge.Application.Reports;
using FoodBridge.Application.Reports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Chart-ready impact reports, one per role. Each action carries its own role policy
/// instead of a shared class-level one, since every action needs a different role.
/// </summary>
[Route("api/reports")]
public sealed class ReportsController : BaseController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [Authorize(Policy = "DonorOnly")]
    [HttpGet("donor")]
    public async Task<ActionResult<ApiResponse<DonorReportResponse>>> GetDonorReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetDonorReportAsync(cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "VolunteerOnly")]
    [HttpGet("volunteer")]
    public async Task<ActionResult<ApiResponse<VolunteerReportResponse>>> GetVolunteerReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetVolunteerReportAsync(cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "RecipientOnly")]
    [HttpGet("recipient")]
    public async Task<ActionResult<ApiResponse<RecipientReportResponse>>> GetRecipientReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetRecipientReportAsync(cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("platform")]
    public async Task<ActionResult<ApiResponse<PlatformReportResponse>>> GetPlatformReport(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetPlatformReportAsync(cancellationToken);
        return HandleResult(result);
    }
}
