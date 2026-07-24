namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection — aggregated VolunteerPoints joined to the volunteer's name, with a computed rank.</summary>
public sealed class LeaderboardEntry
{
    public Guid VolunteerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int TotalDeliveries { get; set; }
    public int Rank { get; set; }
}
