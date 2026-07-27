using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class DropOffLocationRepository : BaseRepository, IDropOffLocationRepository
{
    private const string SelectSql = "SELECT Id, Name, Address, Latitude, Longitude, City, IsActive, CreatedAtUtc, UpdatedAtUtc FROM DropOffLocations";

    public DropOffLocationRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<Guid> CreateAsync(DropOffLocation location, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DropOffLocations (Name, Address, Latitude, Longitude, Location, City, IsActive, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@Name, @Address, @Latitude, @Longitude,
        " + GeoHelper.PointFromLatLngFragment + @",
        @City, @IsActive, @CreatedAtUtc, @UpdatedAtUtc);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, location, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(command);
    }

    public async Task<(IReadOnlyList<DropOffLocation> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var parameters = new { Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM DropOffLocations", cancellationToken: cancellationToken));

        var itemsCommand = new CommandDefinition(
            SelectSql + " ORDER BY Name OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<DropOffLocation>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task<DropOffLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DropOffLocation>(command);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DropOffLocations SET IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id;";
        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, IsActive = isActive }, cancellationToken: cancellationToken));
    }

    public async Task<DropOffLocation?> GetNearestActiveAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT TOP (1) Id, Name, Address, Latitude, Longitude, City, IsActive, CreatedAtUtc, UpdatedAtUtc
FROM DropOffLocations
WHERE IsActive = 1
ORDER BY Location.STDistance({GeoHelper.PointFromLatLngFragment}) ASC;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Latitude = latitude, Longitude = longitude }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DropOffLocation>(command);
    }
}
