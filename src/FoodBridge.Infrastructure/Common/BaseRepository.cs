using System.Data;
using System.Data.Common;
using FoodBridge.Application.Abstractions;

namespace FoodBridge.Infrastructure.Common;

public abstract class BaseRepository
{
    protected readonly IDbConnectionFactory ConnectionFactory;

    protected BaseRepository(IDbConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory;
    }

    protected async Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbConnection, IDbTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await operation(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    protected Task ExecuteInTransactionAsync(
        Func<IDbConnection, IDbTransaction, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            await operation(connection, transaction);
            return true;
        }, cancellationToken);

    private static async Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }
    }
}
