using FoodBridge.Application.Common;
using FoodBridge.Application.Tracking;
using FoodBridge.Application.Tracking.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// REST fallback for live position updates delivered via TrackingHub.
/// </summary>
[Authorize]
[Route("api/listings")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class TrackingController : BaseController
{
    private readonly ITrackingService _trackingService;

    public TrackingController(ITrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    /// <summary>
    /// Last known volunteer location for this listing. Donor, assigned volunteer, or
    /// matched recipient only; returns null data if nothing has been reported yet.
    /// </summary>
    [HttpGet("{id:guid}/track")]
    [ProducesResponseType(typeof(ApiResponse<TrackingResponse?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrackingResponse?>>> GetTracking(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trackingService.GetTrackingAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
