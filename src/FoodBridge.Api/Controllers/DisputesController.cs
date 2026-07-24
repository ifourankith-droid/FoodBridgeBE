using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Disputes;
using FoodBridge.Application.Disputes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Admin dispute moderation. Raising a dispute isn't exposed via any endpoint yet —
/// no earlier phase wired a user-facing "report an issue" flow, so rows currently
/// only exist if inserted directly; resolving them is this phase's actual scope.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("api/disputes")]
public sealed class DisputesController : BaseController
{
    private readonly IDisputeService _disputeService;
    private readonly IValidator<ResolveDisputeRequest> _resolveValidator;

    public DisputesController(IDisputeService disputeService, IValidator<ResolveDisputeRequest> resolveValidator)
    {
        _disputeService = disputeService;
        _resolveValidator = resolveValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<DisputeResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _disputeService.GetAllAsync(status, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    [HttpPatch("{id:guid}/resolve")]
    public async Task<ActionResult<ApiResponse<DisputeResponse>>> Resolve(Guid id, [FromBody] ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        await _resolveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _disputeService.ResolveAsync(id, request, cancellationToken);
        return HandleResult(result);
    }
}
