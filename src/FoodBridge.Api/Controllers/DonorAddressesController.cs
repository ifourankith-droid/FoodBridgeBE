using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.DonorAddresses;
using FoodBridge.Application.DonorAddresses.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// A donor's own saved address book — lets a donor with multiple locations (e.g.
/// restaurant branches) save each one once and pick it on listing creation instead of
/// retyping it every time. Self only throughout, enforced in the service.
/// </summary>
[Authorize(Policy = "DonorOnly")]
[Route("api/donor-addresses")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class DonorAddressesController : BaseController
{
    private readonly IDonorAddressService _donorAddressService;
    private readonly IValidator<CreateDonorAddressRequest> _createValidator;
    private readonly IValidator<UpdateDonorAddressRequest> _updateValidator;

    public DonorAddressesController(
        IDonorAddressService donorAddressService,
        IValidator<CreateDonorAddressRequest> createValidator,
        IValidator<UpdateDonorAddressRequest> updateValidator)
    {
        _donorAddressService = donorAddressService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Saves a new address. Setting <c>isDefault</c> clears it on every other saved address.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DonorAddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DonorAddressResponse>>> Create([FromBody] CreateDonorAddressRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _donorAddressService.CreateAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Lists the caller's own saved addresses, default-first then newest-first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DonorAddressResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DonorAddressResponse>>> GetMyAddresses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _donorAddressService.GetMyAddressesAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DonorAddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DonorAddressResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _donorAddressService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DonorAddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DonorAddressResponse>>> Update(Guid id, [FromBody] UpdateDonorAddressRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _donorAddressService.UpdateAsync(id, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _donorAddressService.DeleteAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
