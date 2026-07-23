using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Donor-side listing management: create, list, detail, update, cancel, image upload.
/// </summary>
[Authorize(Policy = "DonorOnly")]
[Route("api/listings")]
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
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Create([FromBody] CreateListingRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _listingService.CreateAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists the current donor's own listings, optionally filtered by status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ListingSummaryResponse>>> GetMyListings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _listingService.GetMyListingsAsync(page, pageSize, status, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Returns a listing's full detail, including images and timeline. Owning donor only.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _listingService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates a listing. Owning donor only; only while the listing is Pending (422 otherwise).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _listingService.UpdateAsync(id, request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancels a listing. Owning donor only; only while the listing is Pending (422 otherwise).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _listingService.CancelAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Uploads a photo of the food for a listing (JPG/PNG, max 5MB). Owning donor only;
    /// only while the listing is Pending.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
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
