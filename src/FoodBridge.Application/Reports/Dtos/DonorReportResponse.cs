namespace FoodBridge.Application.Reports.Dtos;

public sealed record DonorReportResponse(int TotalListings, int TotalMealsDonated, int TotalCertificates, IReadOnlyList<ChartPoint> MealsDonatedByMonth);
