using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations;
using FoodBridge.Application.DropOffLocations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// The shared pool of places food can be taken: admin-curated partner collection points plus
/// recipient hotspots discovered by volunteers and saved at confirm-delivery.
/// <para>
/// Per-action authorization rather than one class-level policy — CRUD is Admin's, while
/// <c>hotspots</c> is the volunteer's map of where to take what they're carrying. Same pattern
/// <c>ReportsController</c>/<c>DisputesController</c> already use for exactly this reason.
/// </para>
/// </summary>
[Authorize]
[Route("api/dropoff-locations")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class DropOffLocationsController : BaseController
{
    private readonly IDropOffLocationService _dropOffLocationService;
    private readonly IValidator<CreateDropOffLocationRequest> _createValidator;

    public DropOffLocationsController(IDropOffLocationService dropOffLocationService, IValidator<CreateDropOffLocationRequest> createValidator)
    {
        _dropOffLocationService = dropOffLocationService;
        _createValidator = createValidator;
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DropOffLocationResponse>>> Create([FromBody] CreateDropOffLocationRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _dropOffLocationService.CreateAsync(request, cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DropOffLocationResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _dropOffLocationService.GetAllAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Nearby drop-off spots for the volunteer's hotspot map, with usage intensity and cooldown
    /// state. Ordered available-first then nearest, so the first row is where to take what they're
    /// carrying. Spots on cooldown are still returned, flagged, so the map shows why a close spot
    /// isn't being suggested rather than silently omitting it.
    /// </summary>
    [Authorize(Policy = "VolunteerOnly")]
    [HttpGet("hotspots")]
    [ProducesResponseType(typeof(PagedResponse<DropOffHotspotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<DropOffHotspotResponse>>> GetHotspots(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] double? radiusKm = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _dropOffLocationService.GetHotspotsAsync(latitude, longitude, radiusKm, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DropOffLocationResponse>>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dropOffLocationService.ActivateAsync(id, cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DropOffLocationResponse>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dropOffLocationService.DeactivateAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
