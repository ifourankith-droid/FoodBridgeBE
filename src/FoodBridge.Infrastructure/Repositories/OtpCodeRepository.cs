using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class OtpCodeRepository : BaseRepository, IOtpCodeRepository
{
    public OtpCodeRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<int> CountSentSinceAsync(string mobile, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM OtpCodes WHERE Mobile = @Mobile AND CreatedAtUtc >= @SinceUtc";
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Mobile = mobile, SinceUtc = sinceUtc }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task CreateAsync(OtpCode otpCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OtpCodes (Id, Mobile, CodeHash, ExpiresAtUtc, Attempts, ConsumedAtUtc, CreatedAtUtc, UpdatedAtUtc)
VALUES (@Id, @Mobile, @CodeHash, @ExpiresAtUtc, @Attempts, @ConsumedAtUtc, @CreatedAtUtc, @UpdatedAtUtc);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, otpCode, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<OtpCode?> GetLatestActiveAsync(string mobile, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 Id, Mobile, CodeHash, ExpiresAtUtc, Attempts, ConsumedAtUtc, CreatedAtUtc, UpdatedAtUtc
FROM OtpCodes
WHERE Mobile = @Mobile AND ConsumedAtUtc IS NULL AND ExpiresAtUtc > SYSUTCDATETIME()
ORDER BY CreatedAtUtc DESC";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Mobile = mobile }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<OtpCode>(command);
    }

    public async Task IncrementAttemptsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE OtpCodes SET Attempts = Attempts + 1, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id";
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task ConsumeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE OtpCodes SET ConsumedAtUtc = SYSUTCDATETIME(), UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id";
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }
}
