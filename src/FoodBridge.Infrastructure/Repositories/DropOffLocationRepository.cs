using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class DropOffLocationRepository : BaseRepository, IDropOffLocationRepository
{
    private const string Columns = "Id, Name, Address, Latitude, Longitude, City, IsActive, Source, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc";
    private const string SelectSql = "SELECT " + Columns + " FROM DropOffLocations";

    public DropOffLocationRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<Guid> CreateAsync(DropOffLocation location, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(InsertSql, location, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(command);
    }

    /// <summary>
    /// Shared by the admin create endpoint and by confirm-delivery saving a brand-new spot the
    /// volunteer found — the latter runs inside the delivery's own transaction (see
    /// ListingRepository), so the statement has to be reusable rather than owning a connection.
    /// </summary>
    internal const string InsertSql = @"
INSERT INTO DropOffLocations (Name, Address, Latitude, Longitude, Location, City, IsActive, Source, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@Name, @Address, @Latitude, @Longitude,
        " + GeoHelper.PointFromLatLngFragment + @",
        @City, @IsActive, @Source, @CreatedByUserId, @CreatedAtUtc, @UpdatedAtUtc);";

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

    public async Task<DropOffLocation?> GetNearestAvailableAsync(decimal latitude, decimal longitude, DateTime cooldownSinceUtc, CancellationToken cancellationToken = default)
    {
        // NOT EXISTS rather than a join + HAVING: we only care whether *any* delivery landed
        // inside the window, so this short-circuits on the first match using the
        // (DropOffLocationId, DeliveredAtUtc DESC) index.
        var sql = $@"
SELECT TOP (1) {Columns}
FROM DropOffLocations l
WHERE l.IsActive = 1
  AND NOT EXISTS (
      SELECT 1 FROM DropOffDeliveries d
      WHERE d.DropOffLocationId = l.Id AND d.DeliveredAtUtc > @CooldownSinceUtc)
ORDER BY l.Location.STDistance({GeoHelper.PointFromLatLngFragment}) ASC;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Latitude = latitude, Longitude = longitude, CooldownSinceUtc = cooldownSinceUtc }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DropOffLocation>(command);
    }

    public async Task<(IReadOnlyList<DropOffHotspot> Items, int TotalCount)> GetHotspotsAsync(
        decimal latitude,
        decimal longitude,
        double radiusMeters,
        DateTime nowUtc,
        TimeSpan cooldown,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var cooldownSinceUtc = nowUtc - cooldown;
        var parameters = new
        {
            Latitude = latitude,
            Longitude = longitude,
            RadiusMeters = radiusMeters,
            CooldownSinceUtc = cooldownSinceUtc,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize,
        };

        var countSql = $@"
SELECT COUNT(*)
FROM DropOffLocations l
WHERE l.IsActive = 1
  AND l.Location.STDistance({GeoHelper.PointFromLatLngFragment}) <= @RadiusMeters;";

        // Aggregates come from a correlated subquery per row rather than a GROUP BY over a join,
        // so a location with zero deliveries still appears (an admin-added spot nobody has used
        // yet is exactly the kind of thing a volunteer needs to see).
        var itemsSql = $@"
SELECT
    l.Id, l.Name, l.Address, l.Latitude, l.Longitude, l.City, l.IsActive, l.Source, l.CreatedByUserId, l.CreatedAtUtc, l.UpdatedAtUtc,
    l.Location.STDistance({GeoHelper.PointFromLatLngFragment}) / 1000.0 AS DistanceKm,
    ISNULL(agg.DeliveryCount, 0) AS DeliveryCount,
    ISNULL(agg.TotalMeals, 0) AS TotalMeals,
    agg.LastDeliveredAtUtc
FROM DropOffLocations l
OUTER APPLY (
    SELECT COUNT(*) AS DeliveryCount, SUM(d.MealsCount) AS TotalMeals, MAX(d.DeliveredAtUtc) AS LastDeliveredAtUtc
    FROM DropOffDeliveries d
    WHERE d.DropOffLocationId = l.Id
) agg
WHERE l.IsActive = 1
  AND l.Location.STDistance({GeoHelper.PointFromLatLngFragment}) <= @RadiusMeters
ORDER BY
    -- Available spots first, then nearest: the volunteer's best next destination is row 1,
    -- while cooling-down spots still show (labelled) so the map stays informative.
    CASE WHEN agg.LastDeliveredAtUtc > @CooldownSinceUtc THEN 1 ELSE 0 END ASC,
    DistanceKm ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var connection = ConnectionFactory.CreateConnection();

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<HotspotRow>(new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(row => new DropOffHotspot
        {
            Location = new DropOffLocation
            {
                Id = row.Id,
                Name = row.Name,
                Address = row.Address,
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                City = row.City,
                IsActive = row.IsActive,
                Source = (DropOffLocationSource)row.Source,
                CreatedByUserId = row.CreatedByUserId,
                CreatedAtUtc = row.CreatedAtUtc,
                UpdatedAtUtc = row.UpdatedAtUtc,
            },
            DistanceKm = row.DistanceKm,
            DeliveryCount = row.DeliveryCount,
            TotalMeals = row.TotalMeals,
            LastDeliveredAtUtc = row.LastDeliveredAtUtc,
            // Null unless the last delivery falls inside the window — the same condition the
            // ORDER BY uses to sink cooling-down spots below available ones.
            CooldownUntilUtc = row.LastDeliveredAtUtc > cooldownSinceUtc
                ? row.LastDeliveredAtUtc.Value + cooldown
                : null,
        }).ToList();

        return (items, totalCount);
    }

    /// <summary>Flat projection of the hotspot query — Dapper can't map into the nested shape directly.</summary>
    private sealed class HotspotRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public decimal Latitude { get; init; }
        public decimal Longitude { get; init; }
        public string? City { get; init; }
        public bool IsActive { get; init; }
        public byte Source { get; init; }
        public Guid? CreatedByUserId { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public double DistanceKm { get; init; }
        public int DeliveryCount { get; init; }
        public int TotalMeals { get; init; }
        public DateTime? LastDeliveredAtUtc { get; init; }
    }
}
