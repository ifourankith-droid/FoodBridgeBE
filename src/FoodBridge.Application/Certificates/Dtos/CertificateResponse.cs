namespace FoodBridge.Application.Certificates.Dtos;

public sealed record CertificateResponse(
    Guid Id,
    string CertificateNumber,
    Guid ListingId,
    int MealsCount,
    DateTime IssuedAtUtc,
    string? PdfUrl);
