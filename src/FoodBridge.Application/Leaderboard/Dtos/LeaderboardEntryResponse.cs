namespace FoodBridge.Application.Leaderboard.Dtos;

public sealed record LeaderboardEntryResponse(Guid VolunteerId, string Name, int TotalPoints, int TotalDeliveries, int Rank);
