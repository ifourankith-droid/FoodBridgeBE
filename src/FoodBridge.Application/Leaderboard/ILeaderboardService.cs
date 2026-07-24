using FoodBridge.Application.Common;
using FoodBridge.Application.Leaderboard.Dtos;

namespace FoodBridge.Application.Leaderboard;

public interface ILeaderboardService
{
    Task<Result<PagedResult<LeaderboardEntryResponse>>> GetLeaderboardAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Null data if the caller has no VolunteerPoints yet (never delivered).</summary>
    Task<Result<LeaderboardEntryResponse?>> GetMyRankAsync(CancellationToken cancellationToken = default);
}
