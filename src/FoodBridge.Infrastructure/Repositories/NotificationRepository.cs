using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class NotificationRepository : BaseRepository, INotificationRepository
{
    private const string SelectSql = "SELECT Id, UserId, Type, Title, Body, PayloadJson, IsRead, CreatedAtUtc, UpdatedAtUtc FROM Notifications";

    public NotificationRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetForUserAsync(Guid userId, bool? isRead, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE UserId = @UserId";
        var isReadFilterSql = isRead is null ? string.Empty : " AND IsRead = @IsRead";
        var parameters = new { UserId = userId, IsRead = isRead, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Notifications" + whereSql + isReadFilterSql, parameters, cancellationToken: cancellationToken));

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + isReadFilterSql + " ORDER BY CreatedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<Notification>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Notification>(command);
    }

    public async Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Notifications SET IsRead = 1, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id;";
        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
