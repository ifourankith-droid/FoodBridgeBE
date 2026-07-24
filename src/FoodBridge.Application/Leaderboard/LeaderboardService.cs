using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Leaderboard.Dtos;

namespace FoodBridge.Application.Leaderboard;

public sealed class LeaderboardService : ILeaderboardService
{
    private readonly ILeaderboardReader _leaderboardReader;
    private readonly ICurrentUser _currentUser;

    public LeaderboardService(ILeaderboardReader leaderboardReader, ICurrentUser currentUser)
    {
        _leaderboardReader = leaderboardReader;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<LeaderboardEntryResponse>>> GetLeaderboardAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _leaderboardReader.GetTopVolunteersAsync(normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<LeaderboardEntryResponse>(items.Select(e => e.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<LeaderboardEntryResponse?>> GetMyRankAsync(CancellationToken cancellationToken = default)
    {
        var entry = await _leaderboardReader.GetForVolunteerAsync(_currentUser.UserId, cancellationToken);
        var response = entry?.ToResponse();
        return Result.Success(response, response is null ? "You haven't completed a delivery yet." : "Success");
    }
}
