using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Volunteer-side listing actions: browse nearby, claim, confirm pickup, confirm delivery.
/// </summary>
[Authorize(Policy = "VolunteerOnly")]
[Route("api/listings")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class VolunteerListingsController : BaseController
{
    private readonly IVolunteerListingService _volunteerListingService;

    public VolunteerListingsController(IVolunteerListingService volunteerListingService)
    {
        _volunteerListingService = volunteerListingService;
    }

    /// <summary>
    /// Lists Pending listings within <paramref name="radiusKm"/> (default 10, max 50) of the
    /// given coordinates, ordered by ascending distance. Optionally filtered by diet/meal type.
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(PagedResponse<ListingNearbyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ListingNearbyResponse>>> GetNearby(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] string? dietType = null,
        [FromQuery] string? mealType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _volunteerListingService.GetNearbyAsync(latitude, longitude, radiusKm, dietType, mealType, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Claims a Pending listing (Pending → Claimed). Any available volunteer may claim;
    /// under a concurrent race exactly one request succeeds (409 for the loser).
    /// </summary>
    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Claim(Guid id, CancellationToken cancellationToken)
    {
        var result = await _volunteerListingService.ClaimAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Releases a claim (Claimed → Pending), making the listing available for another
    /// volunteer to claim. Assigned volunteer only; only while still Claimed (422 once
    /// pickup has been confirmed — there's no undoing a physical pickup).
    /// </summary>
    [HttpPost("{id:guid}/unclaim")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Unclaim(Guid id, CancellationToken cancellationToken)
    {
        var result = await _volunteerListingService.UnclaimAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Confirms pickup (Claimed → PickedUp) with a required photo. Assigned volunteer only.
    /// </summary>
    [HttpPost("{id:guid}/confirm-pickup")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> ConfirmPickup(Guid id, IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
        {
            return BadRequest(ApiResponse<ListingResponse>.Fail("A pickup photo is required.", traceId: TraceId));
        }

        var extension = Path.GetExtension(photo.FileName);
        await using var stream = photo.OpenReadStream();
        var result = await _volunteerListingService.ConfirmPickupAsync(id, stream, extension, photo.Length, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Confirms delivery (PickedUp → Delivered) with a required photo. Assigned volunteer only.
    /// </summary>
    [HttpPost("{id:guid}/confirm-delivery")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> ConfirmDelivery(Guid id, IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
        {
            return BadRequest(ApiResponse<ListingResponse>.Fail("A delivery photo is required.", traceId: TraceId));
        }

        var extension = Path.GetExtension(photo.FileName);
        await using var stream = photo.OpenReadStream();
        var result = await _volunteerListingService.ConfirmDeliveryAsync(id, stream, extension, photo.Length, cancellationToken);
        return HandleResult(result);
    }
}
