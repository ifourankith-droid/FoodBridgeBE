using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class UserDocumentRepository : BaseRepository, IUserDocumentRepository
{
    private const string SelectSql = "SELECT Id, UserId, Type, FileUrl, OriginalFileName, CreatedAtUtc, UpdatedAtUtc FROM UserDocuments";

    public UserDocumentRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public Task<string?> UpsertAsync(UserDocument document, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            // Read the outgoing URL before overwriting it — once the UPDATE lands, the only
            // record of the old file's location is gone and it would leak on disk forever.
            var previousUrl = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT FileUrl FROM UserDocuments WHERE UserId = @UserId AND Type = @Type;",
                new { document.UserId, Type = (byte)document.Type },
                transaction,
                cancellationToken: cancellationToken));

            if (previousUrl is null)
            {
                const string insertSql = @"
INSERT INTO UserDocuments (UserId, Type, FileUrl, OriginalFileName, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@UserId, @Type, @FileUrl, @OriginalFileName, @CreatedAtUtc, @UpdatedAtUtc);";

                document.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                    insertSql,
                    new { document.UserId, Type = (byte)document.Type, document.FileUrl, document.OriginalFileName, document.CreatedAtUtc, document.UpdatedAtUtc },
                    transaction,
                    cancellationToken: cancellationToken));

                return null;
            }

            const string updateSql = @"
UPDATE UserDocuments
SET FileUrl = @FileUrl, OriginalFileName = @OriginalFileName, UpdatedAtUtc = @UpdatedAtUtc
OUTPUT INSERTED.Id
WHERE UserId = @UserId AND Type = @Type;";

            document.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                updateSql,
                new { document.UserId, Type = (byte)document.Type, document.FileUrl, document.OriginalFileName, document.UpdatedAtUtc },
                transaction,
                cancellationToken: cancellationToken));

            return previousUrl;
        }, cancellationToken);

    public async Task<IReadOnlyList<UserDocument>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(
            SelectSql + " WHERE UserId = @UserId ORDER BY Type",
            new { UserId = userId },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<UserDocument>(command)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<UserDocumentType>>> GetTypesForUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<UserDocumentType>>();
        }

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(
            "SELECT UserId, Type FROM UserDocuments WHERE UserId IN @UserIds;",
            new { UserIds = userIds },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<(Guid UserId, byte Type)>(command);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UserDocumentType>)group.Select(row => (UserDocumentType)row.Type).ToList());
    }
}
