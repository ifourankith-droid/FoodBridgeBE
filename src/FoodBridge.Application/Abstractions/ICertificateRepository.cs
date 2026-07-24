using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface ICertificateRepository
{
    Task<(IReadOnlyList<Certificate> Items, int TotalCount)> GetForDonorAsync(Guid donorId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Certificate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdatePdfUrlAsync(Guid id, string pdfUrl, CancellationToken cancellationToken = default);
}
