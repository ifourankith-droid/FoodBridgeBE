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
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _volunteerListingService.GetNearbyAsync(latitude, longitude, radiusKm, dietType, mealType, status, page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// The signed-in volunteer's claimed listings across every stage (Claimed → Confirmed),
    /// most recently updated first — the data behind the My Deliveries page.
    /// </summary>
    [HttpGet("deliveries")]
    [ProducesResponseType(typeof(PagedResponse<ListingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListingResponse>>> GetMyDeliveries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _volunteerListingService.GetMyDeliveriesAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Claims a Pending listing (Pending → Claimed). Any available volunteer may claim;
    /// under a concurrent race exactly one request succeeds (409 for the loser). An
    /// optional <paramref name="estimatedPickupAtUtc"/> query parameter lets the volunteer
    /// commit to a delayed pickup instead of an implied immediate one (422 if it's in the
    /// past or after the listing's own pickup deadline). Deliberately a query parameter,
    /// not a JSON body — ASP.NET Core's [FromBody] model binding 415s a request with no
    /// Content-Type header at all, which would have broken the "just POST with nothing"
    /// call shape this action has always supported; a query parameter has no such
    /// content-negotiation dependency and stays trivially optional.
    /// </summary>
    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Claim(Guid id, [FromQuery] DateTime? estimatedPickupAtUtc, CancellationToken cancellationToken)
    {
        var result = await _volunteerListingService.ClaimAsync(id, estimatedPickupAtUtc, cancellationToken);
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
    /// Confirms delivery with a required photo. Assigned volunteer only. Ends at
    /// <c>Confirmed</c> when no recipient was matched (completing the donation), or
    /// <c>Delivered</c> when one was and still has to confirm receipt.
    /// </summary>
    /// <remarks>
    /// Alongside the photo, the volunteer records where the food went: either
    /// <c>dropOffLocationId</c> for a spot that already exists, or
    /// <c>latitude</c>/<c>longitude</c>/<c>locationName</c> for one they found in the field, which
    /// is saved so every volunteer can use it next time. Exactly one form, or 422.
    /// <para>
    /// Bound as individual <c>[FromForm]</c> scalars rather than a DTO — see the Phase 13 decision
    /// log on model-binding complex types alongside <c>IFormFile</c> on these actions.
    /// </para>
    /// </remarks>
    [HttpPost("{id:guid}/confirm-delivery")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> ConfirmDelivery(
        Guid id,
        IFormFile? photo,
        [FromForm] Guid? dropOffLocationId,
        [FromForm] decimal? latitude,
        [FromForm] decimal? longitude,
        [FromForm] string? locationName,
        [FromForm] string? locationAddress,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
        {
            return BadRequest(ApiResponse<ListingResponse>.Fail("A delivery photo is required.", traceId: TraceId));
        }

        var dropOff = new DropOffChoice(dropOffLocationId, latitude, longitude, locationName, locationAddress);

        var extension = Path.GetExtension(photo.FileName);
        await using var stream = photo.OpenReadStream();
        var result = await _volunteerListingService.ConfirmDeliveryAsync(id, stream, extension, photo.Length, dropOff, cancellationToken);
        return HandleResult(result);
    }
}
