using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Donor-side listing management: create, list, detail, update, cancel, image upload.
/// </summary>
[Authorize(Policy = "DonorOnly")]
[Route("api/listings")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class ListingsController : BaseController
{
    private readonly IListingService _listingService;
    private readonly IValidator<CreateListingRequest> _createValidator;
    private readonly IValidator<UpdateListingRequest> _updateValidator;

    public ListingsController(
        IListingService listingService,
        IValidator<CreateListingRequest> createValidator,
        IValidator<UpdateListingRequest> updateValidator)
    {
        _listingService = listingService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Creates a new listing, starting in the Pending status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Create([FromBody] CreateListingRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _listingService.CreateAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists the current donor's own listings, optionally filtered by status, diet type, and/or meal type.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListingSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ListingSummaryResponse>>> GetMyListings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? dietType = null,
        [FromQuery] string? mealType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _listingService.GetMyListingsAsync(page, pageSize, status, dietType, mealType, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Returns a listing's full detail, including images and timeline. Owning donor only.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _listingService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates a listing. Owning donor only; only while the listing is Pending (422 otherwise).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _listingService.UpdateAsync(id, request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancels a listing. Owning donor only; only while the listing is Pending (422 otherwise).
    /// </summary>
    /// <summary>
    /// Deliver your own still-unclaimed listing (Pending → Confirmed) rather than wait for a
    /// volunteer. Own listing only. Issues the donor's certificate; awards no volunteer points,
    /// because no volunteer was involved.
    /// </summary>
    /// <remarks>
    /// <c>multipart/form-data</c>: a required <c>photo</c>, plus where it went — either
    /// <c>dropOffLocationId</c> for an existing spot, or
    /// <c>latitude</c>/<c>longitude</c>/<c>locationName</c> for a new one. Exactly one form, the same
    /// contract as the volunteer's confirm-delivery. Returns 409 if a volunteer claims it first.
    /// </remarks>
    [HttpPost("{id:guid}/self-deliver")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> SelfDeliver(
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
        var result = await _listingService.SelfDeliverAsync(id, stream, extension, photo.Length, dropOff, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _listingService.CancelAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Uploads a photo of the food for a listing (JPG/JFIF/PNG/WebP/AVIF/GIF/BMP, max 5MB). Owning donor only;
    /// only while the listing is Pending.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ListingImageUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingImageUploadResponse>>> UploadImage(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<ListingImageUploadResponse>.Fail("A file is required.", traceId: TraceId));
        }

        var extension = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        var result = await _listingService.UploadImageAsync(id, stream, extension, file.Length, cancellationToken);
        return HandleResult(result);
    }
}
