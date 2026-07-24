using FoodBridge.Application.Certificates;
using FoodBridge.Application.Certificates.Dtos;
using FoodBridge.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Donor's own donation certificates. Self only.
/// </summary>
[Authorize(Policy = "DonorOnly")]
[Route("api/certificates")]
public sealed class CertificatesController : BaseController
{
    private readonly ICertificateService _certificateService;

    public CertificatesController(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CertificateResponse>>> GetMyCertificates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _certificateService.GetMyCertificatesAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CertificateResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _certificateService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Downloads the certificate as a PDF. Bypasses the ApiResponse envelope — a binary
    /// file can't be wrapped in JSON; ownership/not-found still go through the shared
    /// exception middleware exactly like every other endpoint.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var pdfBytes = await _certificateService.GetPdfAsync(id, cancellationToken);
        return File(pdfBytes, "application/pdf", $"FoodBridge-Certificate-{id}.pdf");
    }
}
