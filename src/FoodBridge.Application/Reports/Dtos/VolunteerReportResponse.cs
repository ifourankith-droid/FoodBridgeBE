namespace FoodBridge.Application.Reports.Dtos;

public sealed record VolunteerReportResponse(int TotalDeliveries, int TotalPoints, IReadOnlyList<ChartPoint> DeliveriesByMonth);
