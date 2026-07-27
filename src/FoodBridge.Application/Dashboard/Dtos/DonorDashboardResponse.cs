using FoodBridge.Application.Listings.Dtos;
using FoodBridge.Application.Reports.Dtos;

namespace FoodBridge.Application.Dashboard.Dtos;

public sealed record DonorDashboardResponse(
    int TotalMealsDonated,
    int MealsDonatedToday,
    int TotalDonations,
    int TotalCertificates,
    IReadOnlyList<ChartPoint> MealsDonatedByMonth,
    IReadOnlyList<ListingSummaryResponse> RecentActivity,
    IReadOnlyList<NearbyRecipientResponse> NearbyRecipients);
