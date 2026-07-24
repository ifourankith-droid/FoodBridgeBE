using FoodBridge.Application.Certificates.Dtos;
using FoodBridge.Application.Common;

namespace FoodBridge.Application.Certificates;

public interface ICertificateService
{
    /// <summary>Self only.</summary>
    Task<Result<PagedResult<CertificateResponse>>> GetMyCertificatesAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Self only.</summary>
    Task<Result<CertificateResponse>> GetByIdAsync(Guid certificateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Self only. Renders fresh PDF bytes every call (cheap, in-memory); lazily persists a
    /// copy via IFileStorage and records Certificates.PdfUrl the first time only. No
    /// Result wrapper — ownership/not-found are thrown exceptions, and a binary file
    /// response can't be wrapped in the ApiResponse envelope anyway.
    /// </summary>
    Task<byte[]> GetPdfAsync(Guid certificateId, CancellationToken cancellationToken = default);
}
