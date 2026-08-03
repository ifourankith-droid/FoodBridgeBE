using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class DonorAddressRepository : BaseRepository, IDonorAddressRepository
{
    private const string SelectSql = "SELECT Id, DonorId, Label, Address, City, State, Pincode, Latitude, Longitude, IsDefault, CreatedAtUtc, UpdatedAtUtc FROM DonorAddresses";

    public DonorAddressRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<Guid> CreateAsync(DonorAddress address, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DonorAddresses (DonorId, Label, Address, City, State, Pincode, Latitude, Longitude, IsDefault, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@DonorId, @Label, @Address, @City, @State, @Pincode, @Latitude, @Longitude, @IsDefault, @CreatedAtUtc, @UpdatedAtUtc);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, address, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(command);
    }

    public async Task<DonorAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DonorAddress>(command);
    }

    public async Task<(IReadOnlyList<DonorAddress> Items, int TotalCount)> GetByDonorAsync(Guid donorId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE DonorId = @DonorId";
        var parameters = new { DonorId = donorId, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM DonorAddresses" + whereSql, parameters, cancellationToken: cancellationToken));

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + " ORDER BY IsDefault DESC, CreatedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<DonorAddress>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task<DonorAddress?> GetDefaultAsync(Guid donorId, CancellationToken cancellationToken = default)
    {
        // Same ordering as GetByDonorAsync, so "the default" here is always the row a donor sees at
        // the top of their address list — including when none is flagged.
        const string sql = " WHERE DonorId = @DonorId ORDER BY IsDefault DESC, CreatedAtUtc DESC";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + sql, new { DonorId = donorId }, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<DonorAddress>(command);
    }

    public async Task UpdateAsync(DonorAddress address, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DonorAddresses SET
    Label = @Label,
    Address = @Address,
    City = @City,
    State = @State,
    Pincode = @Pincode,
    Latitude = @Latitude,
    Longitude = @Longitude,
    IsDefault = @IsDefault,
    UpdatedAtUtc = @UpdatedAtUtc
WHERE Id = @Id;";

        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, address, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM DonorAddresses WHERE Id = @Id;", new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task ClearDefaultAsync(Guid donorId, Guid exceptAddressId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DonorAddresses SET IsDefault = 0 WHERE DonorId = @DonorId AND Id <> @ExceptAddressId;";

        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { DonorId = donorId, ExceptAddressId = exceptAddressId }, cancellationToken: cancellationToken));
    }
}
