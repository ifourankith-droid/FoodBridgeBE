using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Application.Reports.Dtos;

namespace FoodBridge.Application.Dashboard.Dtos;

public sealed record VolunteerDashboardResponse(
    int TotalDeliveries,
    int TotalPoints,
    int? LeaderboardRank,
    int TotalMealsHelped,
    IReadOnlyList<ChartPoint> DeliveriesByMonth,
    IReadOnlyList<BadgeResponse> Badges,
    IReadOnlyList<ListingNearbyResponse> OpenListingsNearby);
