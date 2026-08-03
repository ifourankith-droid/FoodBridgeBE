namespace FoodBridge.Application.Abstractions;

/// <param name="DonorCity">Optional — omitted from the certificate when the donor has no city.</param>
/// <param name="FoodType">Optional — names what was donated, e.g. "Mixed Veg Meals".</param>
/// <param name="IssuedAtUtc">
/// UTC, as stored. The renderer converts it to IST for the printed date: a certificate issued at
/// 21:00 IST is 15:30 UTC the same day, but one issued at 02:00 IST is 20:30 UTC the *previous* day,
/// and printing that would date the certificate a day early.
/// </param>
public sealed record CertificatePdfModel(
    string CertificateNumber,
    string DonorName,
    string ListingTitle,
    int MealsCount,
    DateTime IssuedAtUtc,
    string? DonorCity = null,
    string? FoodType = null);

/// <summary>Swap the QuestPDF implementation for another renderer without touching consumers.</summary>
public interface IPdfGenerator
{
    byte[] GenerateCertificatePdf(CertificatePdfModel model);
}
