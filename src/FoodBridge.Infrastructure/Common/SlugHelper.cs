namespace FoodBridge.Infrastructure.Common;

/// <summary>Builds certificate numbers in the "FB-{yyyyMM}-{seq:D5}" format.</summary>
public static class SlugHelper
{
    public static string BuildCertificateNumber(DateTime issuedAtUtc, int sequenceForMonth) =>
        $"FB-{issuedAtUtc:yyyyMM}-{sequenceForMonth:D5}";
}
