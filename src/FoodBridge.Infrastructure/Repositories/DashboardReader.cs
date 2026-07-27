using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class DashboardReader : BaseRepository, IDashboardReader
{
    public DashboardReader(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<int> GetDonorMealsDonatedTodayAsync(Guid donorId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COALESCE(SUM(QuantityMeals), 0)
FROM Listings
WHERE DonorId = @DonorId AND Status = @ConfirmedStatus AND IsDeleted = 0
    AND CAST(UpdatedAtUtc AS DATE) = CAST(@NowUtc AS DATE);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { DonorId = donorId, ConfirmedStatus = (byte)ListingStatus.Confirmed, NowUtc = nowUtc }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<IReadOnlyList<NearbyRecipient>> GetNearbyRecipientsAsync(decimal latitude, decimal longitude, double radiusMeters, int limit, CancellationToken cancellationToken = default)
    {
        var distanceSql = $"Location.STDistance({GeoHelper.PointFromLatLngFragment})";
        var sql = $@"
SELECT TOP (@Limit) Id, Name, Address, City, Latitude, Longitude, CapacityMeals, {distanceSql} AS DistanceMeters
FROM Users
WHERE Role = @RecipientRole AND AccountStatus = @VerifiedStatus AND IsDeleted = 0
    AND Location IS NOT NULL AND {distanceSql} <= @RadiusMeters
ORDER BY DistanceMeters ASC;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                Latitude = latitude,
                Longitude = longitude,
                RadiusMeters = radiusMeters,
                Limit = limit,
                RecipientRole = (byte)UserRole.Recipient,
                VerifiedStatus = (byte)AccountStatus.Verified,
            },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<NearbyRecipient>(command)).ToList();
    }

    public async Task<int> GetVolunteerMealsHelpedAsync(Guid volunteerId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COALESCE(SUM(QuantityMeals), 0)
FROM Listings
WHERE VolunteerId = @VolunteerId AND Status = @ConfirmedStatus AND IsDeleted = 0;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { VolunteerId = volunteerId, ConfirmedStatus = (byte)ListingStatus.Confirmed }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> GetRecipientMealsReceivedTodayAsync(Guid recipientId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COALESCE(SUM(QuantityMeals), 0)
FROM Listings
WHERE RecipientId = @RecipientId AND Status = @ConfirmedStatus AND IsDeleted = 0
    AND CAST(UpdatedAtUtc AS DATE) = CAST(@NowUtc AS DATE);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { RecipientId = recipientId, ConfirmedStatus = (byte)ListingStatus.Confirmed, NowUtc = nowUtc }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<IReadOnlyList<DonorMealShare>> GetRecipientDonorDistributionAsync(Guid recipientId, int limit, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (@Limit) l.DonorId, u.Name AS DonorName, SUM(l.QuantityMeals) AS TotalMealsReceived
FROM Listings l
JOIN Users u ON u.Id = l.DonorId
WHERE l.RecipientId = @RecipientId AND l.Status = @ConfirmedStatus AND l.IsDeleted = 0
GROUP BY l.DonorId, u.Name
ORDER BY TotalMealsReceived DESC;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { RecipientId = recipientId, ConfirmedStatus = (byte)ListingStatus.Confirmed, Limit = limit }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<DonorMealShare>(command)).ToList();
    }
}
