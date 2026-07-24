namespace FoodBridge.Application.Listings.Dtos;

public sealed record ConfirmReceiptResponse(ListingResponse Listing, string CertificateNumber, int PointsAwarded);
