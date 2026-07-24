using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class DisputeRepository : BaseRepository, IDisputeRepository
{
    private const string SelectSql = "SELECT Id, ListingId, RaisedByUserId, Reason, Status, ResolvedByUserId, ResolutionNote, CreatedAtUtc, UpdatedAtUtc FROM Disputes";

    public DisputeRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<(IReadOnlyList<Dispute> Items, int TotalCount)> GetAllAsync(DisputeStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        var whereSql = status is null ? string.Empty : " WHERE Status = @Status";
        var parameters = new { Status = status, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Disputes" + whereSql, parameters, cancellationToken: cancellationToken));

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + " ORDER BY CreatedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<Dispute>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task<Dispute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Dispute>(command);
    }

    public async Task ResolveAsync(Guid id, Guid resolvedByUserId, string resolutionNote, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Disputes SET
    Status = @ResolvedStatus,
    ResolvedByUserId = @ResolvedByUserId,
    ResolutionNote = @ResolutionNote,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE Id = @Id;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { Id = id, ResolvedByUserId = resolvedByUserId, ResolutionNote = resolutionNote, ResolvedStatus = (byte)DisputeStatus.Resolved },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }
}
