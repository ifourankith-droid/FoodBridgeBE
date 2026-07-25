using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Disputes;
using FoodBridge.Application.Disputes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Dispute raising (any involved party) and admin moderation (list/resolve). Each action
/// carries its own authorization instead of a shared class-level policy, since raising a
/// dispute is open to any authenticated role while list/resolve stay Admin-only.
/// </summary>
[Authorize]
[Route("api/disputes")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class DisputesController : BaseController
{
    private readonly IDisputeService _disputeService;
    private readonly IValidator<CreateDisputeRequest> _createValidator;
    private readonly IValidator<ResolveDisputeRequest> _resolveValidator;

    public DisputesController(IDisputeService disputeService, IValidator<CreateDisputeRequest> createValidator, IValidator<ResolveDisputeRequest> resolveValidator)
    {
        _disputeService = disputeService;
        _createValidator = createValidator;
        _resolveValidator = resolveValidator;
    }

    /// <summary>
    /// Raises a dispute about a listing. Callable by the listing's donor, assigned
    /// volunteer, or matched recipient only (403 for anyone else).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DisputeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DisputeResponse>>> Create([FromBody] CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _disputeService.CreateAsync(request, cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DisputeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DisputeResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _disputeService.GetAllAsync(status, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{id:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<DisputeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<DisputeResponse>>> Resolve(Guid id, [FromBody] ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        await _resolveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _disputeService.ResolveAsync(id, request, cancellationToken);
        return HandleResult(result);
    }
}
