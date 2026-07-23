using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
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

    public async Task<Guid> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Users (Mobile, Name, Role, City, Address, Latitude, Longitude, Location, RecipientType, CapacityMeals, IsAvailable, AccountStatus, AvatarUrl, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@Mobile, @Name, @Role, @City, @Address, @Latitude, @Longitude,
        CASE WHEN @Latitude IS NOT NULL AND @Longitude IS NOT NULL THEN geography::Point(@Latitude, @Longitude, 4326) ELSE NULL END,
        @RecipientType, @CapacityMeals, @IsAvailable, @AccountStatus, @AvatarUrl, @IsDeleted, @CreatedAtUtc, @UpdatedAtUtc);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, user, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(command);
    }
}
