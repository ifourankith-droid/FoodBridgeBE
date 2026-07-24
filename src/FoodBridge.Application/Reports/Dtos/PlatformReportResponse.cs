namespace FoodBridge.Application.Reports.Dtos;

public sealed record PlatformReportResponse(int TotalMealsDonated, int TotalDeliveries, int TotalCertificates, int TotalUsers, IReadOnlyList<ChartPoint> MealsDonatedByMonth);
