using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class RecipientReader : BaseRepository, IRecipientReader
{
    public RecipientReader(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<Guid?> FindNearestAvailableRecipientIdAsync(decimal latitude, decimal longitude, IReadOnlyCollection<Guid>? excludeRecipientIds = null, CancellationToken cancellationToken = default)
    {
        var excludeClause = excludeRecipientIds is { Count: > 0 } ? "AND Id NOT IN @ExcludeRecipientIds" : string.Empty;
        var sql = $@"
SELECT TOP (1) Id
FROM Users
WHERE Role = @RecipientRole AND AccountStatus = @VerifiedStatus AND IsAvailable = 1 AND IsDeleted = 0 AND Location IS NOT NULL
    {excludeClause}
ORDER BY Location.STDistance({GeoHelper.PointFromLatLngFragment}) ASC;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                Latitude = latitude,
                Longitude = longitude,
                RecipientRole = (byte)UserRole.Recipient,
                VerifiedStatus = (byte)AccountStatus.Verified,
                ExcludeRecipientIds = excludeRecipientIds,
            },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<Guid?>(command);
    }
}
