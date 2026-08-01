using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class UserRepository : BaseRepository, IUserRepository
{
    private const string SelectSql = @"
SELECT Id, Mobile, Name, Role, City, Address, Latitude, Longitude, RecipientType, CapacityMeals, IsAvailable, AccountStatus, AvatarUrl, IsDeleted, CreatedAtUtc, UpdatedAtUtc
FROM Users";

    public UserRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<User>(command);
    }

    public async Task<User?> GetByMobileAsync(string mobile, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Mobile = @Mobile AND IsDeleted = 0", new { Mobile = mobile }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<User>(command);
    }

    public Task<Guid> CreateAsync(User user, DonorAddress? homeAddress = null, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string insertUserSql = @"
INSERT INTO Users (Mobile, Name, Role, City, Address, Latitude, Longitude, Location, RecipientType, CapacityMeals, IsAvailable, AccountStatus, AvatarUrl, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@Mobile, @Name, @Role, @City, @Address, @Latitude, @Longitude,
        CASE WHEN @Latitude IS NOT NULL AND @Longitude IS NOT NULL THEN geography::Point(@Latitude, @Longitude, 4326) ELSE NULL END,
        @RecipientType, @CapacityMeals, @IsAvailable, @AccountStatus, @AvatarUrl, @IsDeleted, @CreatedAtUtc, @UpdatedAtUtc);";

            var userId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertUserSql, user, transaction, cancellationToken: cancellationToken));
            user.Id = userId;

            if (homeAddress is not null)
            {
                // DonorId isn't knowable until the user row exists, so it's stamped here rather
                // than by the caller — the whole reason both writes share one transaction.
                homeAddress.DonorId = userId;

                const string insertAddressSql = @"
INSERT INTO DonorAddresses (DonorId, Label, Address, Latitude, Longitude, IsDefault, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@DonorId, @Label, @Address, @Latitude, @Longitude, @IsDefault, @CreatedAtUtc, @UpdatedAtUtc);";

                homeAddress.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertAddressSql, homeAddress, transaction, cancellationToken: cancellationToken));
            }

            return userId;
        }, cancellationToken);

    public async Task UpdateProfileAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Users SET
    Name = @Name,
    City = @City,
    Address = @Address,
    Latitude = @Latitude,
    Longitude = @Longitude,
    Location = CASE WHEN @Latitude IS NOT NULL AND @Longitude IS NOT NULL THEN geography::Point(@Latitude, @Longitude, 4326) ELSE NULL END,
    CapacityMeals = @CapacityMeals,
    UpdatedAtUtc = @UpdatedAtUtc
WHERE Id = @Id AND IsDeleted = 0;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, user, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task UpdateAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Users SET IsAvailable = @IsAvailable, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0;";
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Id = id, IsAvailable = isAvailable }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task UpdateAvatarUrlAsync(Guid id, string avatarUrl, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Users SET AvatarUrl = @AvatarUrl, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0;";
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Id = id, AvatarUrl = avatarUrl }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public Task UpdateAccountStatusAsync(Guid id, AccountStatus accountStatus, Notification? notification = null, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string sql = "UPDATE Users SET AccountStatus = @AccountStatus, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0;";
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id, AccountStatus = (byte)accountStatus },
                transaction,
                cancellationToken: cancellationToken));

            if (notification is not null)
            {
                const string insertNotificationSql = @"
INSERT INTO Notifications (UserId, Type, Title, Body, PayloadJson, IsRead, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@UserId, @Type, @Title, @Body, @PayloadJson, @IsRead, @CreatedAtUtc, @UpdatedAtUtc);";

                notification.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                    insertNotificationSql,
                    notification,
                    transaction,
                    cancellationToken: cancellationToken));
            }
        }, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetNearbyAvailableVolunteerIdsAsync(decimal latitude, decimal longitude, double radiusMeters, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT Id
FROM Users
WHERE Role = @Role AND IsAvailable = 1 AND AccountStatus = @VerifiedStatus AND IsDeleted = 0
    AND Location IS NOT NULL AND Location.STDistance({GeoHelper.PointFromLatLngFragment}) <= @RadiusMeters;";

        var parameters = new
        {
            Latitude = latitude,
            Longitude = longitude,
            RadiusMeters = radiusMeters,
            Role = (byte)UserRole.Volunteer,
            VerifiedStatus = (byte)AccountStatus.Verified,
        };

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<Guid>(command)).ToList();
    }
}
