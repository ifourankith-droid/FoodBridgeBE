using FoodBridge.Application.Certificates.Dtos;
using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Certificates;

public static class CertificateMapper
{
    public static CertificateResponse ToResponse(this Certificate certificate) => new(
        certificate.Id,
        certificate.CertificateNumber,
        certificate.ListingId,
        certificate.MealsCount,
        certificate.IssuedAtUtc,
        certificate.PdfUrl);
}
