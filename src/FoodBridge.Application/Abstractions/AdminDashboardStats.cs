namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection for the admin dashboard's at-a-glance counts.</summary>
public sealed class AdminDashboardStats
{
    public int TotalDonors { get; set; }
    public int TotalVolunteers { get; set; }
    public int TotalRecipients { get; set; }
    public int PendingRecipients { get; set; }
    public int TotalListings { get; set; }
    public int PendingListings { get; set; }
    public int ActiveListings { get; set; }
    public int ConfirmedListings { get; set; }
    public int TotalMealsDonated { get; set; }
    public int TotalCertificatesIssued { get; set; }
    public int TotalVolunteerPointsAwarded { get; set; }
    public int OpenDisputes { get; set; }
    public int ResolvedDisputes { get; set; }
}
