using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Application.Reports.Dtos;

namespace FoodBridge.Application.Dashboard.Dtos;

public sealed record RecipientDashboardResponse(
    int TotalMealsReceived,
    int TotalDeliveriesReceived,
    int MealsReceivedToday,
    int UpcomingDeliveries,
    int? StorageCapacityMeals,
    double? StorageUsedPercentToday,
    IReadOnlyList<ChartPoint> MealsReceivedByMonth,
    IReadOnlyList<DonorMealShareResponse> DonorDistribution,
    IReadOnlyList<ListingSummaryResponse> IncomingFood);
