using FoodBridge.Application.Common;
using FoodBridge.Application.Listings;
using FoodBridge.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Recipient-side listing actions: incoming matches, accept, reject, confirm receipt, history.
/// </summary>
[Authorize(Policy = "RecipientOnly")]
[Route("api/listings")]
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
    public async Task<ActionResult<ApiResponse<ListingResponse>>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var result = await _recipientListingService.AcceptAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Rejects an incoming match. Auto-reassigns to another available recipient if one exists.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
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
    public async Task<ActionResult<ApiResponse<ConfirmReceiptResponse>>> ConfirmReceipt(Guid id, CancellationToken cancellationToken)
    {
        var result = await _recipientListingService.ConfirmReceiptAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists the caller's past confirmed receipts.
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<PagedResponse<ListingSummaryResponse>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _recipientListingService.GetHistoryAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }
}
