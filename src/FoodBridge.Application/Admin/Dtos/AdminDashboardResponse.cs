namespace FoodBridge.Application.Admin.Dtos;

public sealed record AdminDashboardResponse(
    int TotalDonors,
    int TotalVolunteers,
    int TotalRecipients,
    int PendingRecipients,
    int TotalListings,
    int PendingListings,
    int ActiveListings,
    int ConfirmedListings,
    int TotalMealsDonated,
    int TotalCertificatesIssued,
    int TotalVolunteerPointsAwarded,
    int OpenDisputes,
    int ResolvedDisputes);
