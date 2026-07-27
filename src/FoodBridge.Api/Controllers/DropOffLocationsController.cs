using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations;
using FoodBridge.Application.DropOffLocations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Admin-managed fallback pickup destinations. Not browsed directly by volunteers —
/// the nearest active one is automatically suggested on a listing when no recipient
/// is available (see ListingResponse.SuggestedDropOffLocation).
/// </summary>
[Authorize(Policy = "AdminOnly")]
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

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DropOffLocationResponse>>> Create([FromBody] CreateDropOffLocationRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _dropOffLocationService.CreateAsync(request, cancellationToken);
        return HandleResult(result);
    }

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

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DropOffLocationResponse>>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dropOffLocationService.ActivateAsync(id, cancellationToken);
        return HandleResult(result);
    }

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<DropOffLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DropOffLocationResponse>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dropOffLocationService.DeactivateAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
