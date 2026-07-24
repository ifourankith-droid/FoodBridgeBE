using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Certificates.Dtos;
using FoodBridge.Application.Common;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Certificates;

public sealed class CertificateService : ICertificateService
{
    private readonly ICertificateRepository _certificateRepository;
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;

    public CertificateService(
        ICertificateRepository certificateRepository,
        IListingRepository listingRepository,
        IUserRepository userRepository,
        IPdfGenerator pdfGenerator,
        IFileStorage fileStorage,
        ICurrentUser currentUser)
    {
        _certificateRepository = certificateRepository;
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _pdfGenerator = pdfGenerator;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<CertificateResponse>>> GetMyCertificatesAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _certificateRepository.GetForDonorAsync(_currentUser.UserId, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<CertificateResponse>(items.Select(c => c.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<CertificateResponse>> GetByIdAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        var certificate = await GetOwnedCertificateOrThrowAsync(certificateId, cancellationToken);
        return Result.Success(certificate.ToResponse());
    }

    public async Task<byte[]> GetPdfAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        var certificate = await GetOwnedCertificateOrThrowAsync(certificateId, cancellationToken);

        var listing = await _listingRepository.GetByIdAsync(certificate.ListingId, cancellationToken)
            ?? throw new NotFoundException("Listing", certificate.ListingId);
        var donor = await _userRepository.GetByIdAsync(certificate.DonorId, cancellationToken)
            ?? throw new NotFoundException("User", certificate.DonorId);

        var pdfBytes = _pdfGenerator.GenerateCertificatePdf(new CertificatePdfModel(
            certificate.CertificateNumber,
            donor.Name,
            listing.Title,
            certificate.MealsCount,
            certificate.IssuedAtUtc));

        if (certificate.PdfUrl is null)
        {
            using var stream = new MemoryStream(pdfBytes);
            var pdfUrl = await _fileStorage.SaveAsync(stream, ".pdf", cancellationToken);
            await _certificateRepository.UpdatePdfUrlAsync(certificateId, pdfUrl, cancellationToken);
        }

        return pdfBytes;
    }

    private async Task<Certificate> GetOwnedCertificateOrThrowAsync(Guid certificateId, CancellationToken cancellationToken)
    {
        var certificate = await _certificateRepository.GetByIdAsync(certificateId, cancellationToken);
        if (certificate is null)
        {
            throw new NotFoundException("Certificate", certificateId);
        }

        if (certificate.DonorId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only access your own certificates.");
        }

        return certificate;
    }
}
