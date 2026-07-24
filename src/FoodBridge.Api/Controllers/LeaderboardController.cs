using FoodBridge.Application.Common;
using FoodBridge.Application.Leaderboard;
using FoodBridge.Application.Leaderboard.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Volunteer leaderboard, ranked by total VolunteerPoints. Viewable by any authenticated role.
/// </summary>
[Authorize]
[Route("api/leaderboard")]
public sealed class LeaderboardController : BaseController
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<LeaderboardEntryResponse>>> GetLeaderboard(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaderboardService.GetLeaderboardAsync(page, pageSize, cancellationToken);
        return HandlePagedResult(result);
    }

    /// <summary>The caller's own rank. Volunteer only.</summary>
    [Authorize(Policy = "VolunteerOnly")]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<LeaderboardEntryResponse?>>> GetMyRank(CancellationToken cancellationToken)
    {
        var result = await _leaderboardService.GetMyRankAsync(cancellationToken);
        return HandleResult(result);
    }
}
