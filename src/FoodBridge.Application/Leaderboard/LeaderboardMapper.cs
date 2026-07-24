using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Leaderboard.Dtos;

namespace FoodBridge.Application.Leaderboard;

public static class LeaderboardMapper
{
    public static LeaderboardEntryResponse ToResponse(this LeaderboardEntry entry) => new(
        entry.VolunteerId,
        entry.Name,
        entry.TotalPoints,
        entry.TotalDeliveries,
        entry.Rank);
}
