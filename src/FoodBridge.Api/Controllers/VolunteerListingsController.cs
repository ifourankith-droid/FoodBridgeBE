using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Volunteer-side listing actions: browse nearby, claim, confirm pickup, confirm delivery.
/// </summary>
[Authorize(Policy = "VolunteerOnly")]
[Route("api/listings")]
public sealed class VolunteerListingsController : BaseController
{
    private readonly IVolunteerListingService _volunteerListingService;

    public VolunteerListingsController(IVolunteerListingService volunteerListingService)
    {
        _volunteerListingService = volunteerListingService;
    }

    /// <summary>
    /// Lists Pending listings within <paramref name="radiusKm"/> (default 10, max 50) of the
    /// given coordinates, ordered by ascending distance.
    /// </summary>
    [HttpGet("nearby")]
    public async Task<ActionResult<PagedResponse<ListingNearbyResponse>>> GetNearby(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _volunteerListingService.GetNearbyAsync(latitude, longitude, radiusKm, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Claims a Pending listing (Pending → Claimed). Any available volunteer may claim;
    /// under a concurrent race exactly one request succeeds (409 for the loser).
    /// </summary>
    [HttpPost("{id:guid}/claim")]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Claim(Guid id, CancellationToken cancellationToken)
    {
        var result = await _volunteerListingService.ClaimAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Confirms pickup (Claimed → PickedUp) with a required photo. Assigned volunteer only.
    /// </summary>
    [HttpPost("{id:guid}/confirm-pickup")]
    [Consumes("multipart/form-data")]
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
