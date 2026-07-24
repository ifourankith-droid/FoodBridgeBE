namespace FoodBridge.Application.Reports.Dtos;

public sealed record RecipientReportResponse(int TotalMealsReceived, int TotalDeliveriesReceived, IReadOnlyList<ChartPoint> MealsReceivedByMonth);
