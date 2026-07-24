namespace FoodBridge.Application.Abstractions;

public sealed record CertificatePdfModel(string CertificateNumber, string DonorName, string ListingTitle, int MealsCount, DateTime IssuedAtUtc);

/// <summary>Swap the QuestPDF implementation for another renderer without touching consumers.</summary>
public interface IPdfGenerator
{
    byte[] GenerateCertificatePdf(CertificatePdfModel model);
}
