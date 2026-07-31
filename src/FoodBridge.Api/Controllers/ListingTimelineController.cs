using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Read-only lifecycle timeline for a single listing, available to any authenticated
/// user (donor, volunteer, recipient) so every section renders the same steps from
/// one endpoint. Kept separate from the role-scoped listing controllers precisely so
/// it can carry a broader authorization policy.
/// </summary>
[Authorize]
[Route("api/listings")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class ListingTimelineController : BaseController
{
    private readonly IListingService _listingService;

    public ListingTimelineController(IListingService listingService)
    {
        _listingService = listingService;
    }

    /// <summary>
    /// The listing's status changes in order — each with who did it, when, and any note
    /// or proof photo captured at that step.
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ListingTimelineEventResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ListingTimelineEventResponse>>>> GetTimeline(Guid id, CancellationToken cancellationToken)
    {
        var result = await _listingService.GetTimelineAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
