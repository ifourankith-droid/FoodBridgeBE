namespace FoodBridge.Domain.Entities;

public sealed class Certificate
{
    public Guid Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public Guid DonorId { get; set; }
    public Guid ListingId { get; set; }
    public int MealsCount { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
