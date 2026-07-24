using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class LeaderboardReader : BaseRepository, ILeaderboardReader
{
    private const string RankedCte = @"
WITH Aggregated AS (
    SELECT vp.VolunteerId, u.Name, SUM(vp.Points) AS TotalPoints, COUNT(*) AS TotalDeliveries
    FROM VolunteerPoints vp
    JOIN Users u ON u.Id = vp.VolunteerId
    GROUP BY vp.VolunteerId, u.Name
),
Ranked AS (
    SELECT VolunteerId, Name, TotalPoints, TotalDeliveries, RANK() OVER (ORDER BY TotalPoints DESC) AS Rank
    FROM Aggregated
)";

    public LeaderboardReader(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<(IReadOnlyList<LeaderboardEntry> Items, int TotalCount)> GetTopVolunteersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(DISTINCT VolunteerId) FROM VolunteerPoints;", cancellationToken: cancellationToken));

        var itemsSql = RankedCte + " SELECT VolunteerId, Name, TotalPoints, TotalDeliveries, Rank FROM Ranked ORDER BY Rank OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        var items = (await connection.QueryAsync<LeaderboardEntry>(new CommandDefinition(
            itemsSql,
            new { Offset = (page - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken))).ToList();

        return (items, totalCount);
    }

    public async Task<LeaderboardEntry?> GetForVolunteerAsync(Guid volunteerId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var sql = RankedCte + " SELECT VolunteerId, Name, TotalPoints, TotalDeliveries, Rank FROM Ranked WHERE VolunteerId = @VolunteerId;";
        var command = new CommandDefinition(sql, new { VolunteerId = volunteerId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LeaderboardEntry>(command);
    }
}
