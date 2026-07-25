using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Recipient-side listing actions: incoming matches, accept, reject, confirm receipt, history.
/// </summary>
[Authorize(Policy = "RecipientOnly")]
[Route("api/listings")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class RecipientListingsController : BaseController
{
    private readonly IRecipientListingService _recipientListingService;

    public RecipientListingsController(IRecipientListingService recipientListingService)
    {
        _recipientListingService = recipientListingService;
    }

    /// <summary>
    /// Lists listings currently matched to the caller and awaiting an accept/reject decision.
    /// </summary>
    [HttpGet("incoming")]
    [ProducesResponseType(typeof(PagedResponse<ListingSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListingSummaryResponse>>> GetIncoming(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _recipientListingService.GetIncomingAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Accepts an incoming match. Doesn't change the listing's status — just records the acceptance.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var result = await _recipientListingService.AcceptAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Rejects an incoming match. Auto-reassigns to another available recipient if one exists.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Reject(Guid id, CancellationToken cancellationToken)
    {
        var result = await _recipientListingService.RejectAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Confirms receipt (Delivered → Confirmed). Atomically awards volunteer points, issues a
    /// donor certificate, and creates notifications.
    /// </summary>
    [HttpPost("{id:guid}/confirm-receipt")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmReceiptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ConfirmReceiptResponse>>> ConfirmReceipt(Guid id, CancellationToken cancellationToken)
    {
        var result = await _recipientListingService.ConfirmReceiptAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists the caller's past confirmed receipts.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResponse<ListingSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListingSummaryResponse>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _recipientListingService.GetHistoryAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }
}
