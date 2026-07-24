namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Read-only view over VolunteerPoints — split from any write-side repository since
/// leaderboard ranking is a pure aggregate query, not a VolunteerPoints write concern.
/// </summary>
public interface ILeaderboardReader
{
    Task<(IReadOnlyList<LeaderboardEntry> Items, int TotalCount)> GetTopVolunteersAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Null if the volunteer has no VolunteerPoints rows yet (never delivered).</summary>
    Task<LeaderboardEntry?> GetForVolunteerAsync(Guid volunteerId, CancellationToken cancellationToken = default);
}
