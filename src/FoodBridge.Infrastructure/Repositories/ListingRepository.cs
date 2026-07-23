using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class ListingRepository : BaseRepository, IListingRepository
{
    private const string SelectSql = @"
SELECT Id, DonorId, Title, FoodType, DietType, MealType, QuantityMeals, FreshnessTag, PreparedAtUtc, PickupDeadlineUtc, PickupAddress, Latitude, Longitude, Status, VolunteerId, RecipientId, IsDeleted, CreatedAtUtc, UpdatedAtUtc
FROM Listings";

    public ListingRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public Task<Guid> CreateAsync(Listing listing, ListingTimelineEvent creationEvent, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string insertListingSql = @"
INSERT INTO Listings (DonorId, Title, FoodType, DietType, MealType, QuantityMeals, FreshnessTag, PreparedAtUtc, PickupDeadlineUtc, PickupAddress, Latitude, Longitude, Location, Status, VolunteerId, RecipientId, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@DonorId, @Title, @FoodType, @DietType, @MealType, @QuantityMeals, @FreshnessTag, @PreparedAtUtc, @PickupDeadlineUtc, @PickupAddress, @Latitude, @Longitude,
        geography::Point(@Latitude, @Longitude, 4326),
        @Status, @VolunteerId, @RecipientId, @IsDeleted, @CreatedAtUtc, @UpdatedAtUtc);";

            var listingId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertListingSql, listing, transaction, cancellationToken: cancellationToken));
            listing.Id = listingId;
            creationEvent.ListingId = listingId;

            const string insertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";

            await connection.ExecuteAsync(new CommandDefinition(insertTimelineSql, creationEvent, transaction, cancellationToken: cancellationToken));

            return listingId;
        }, cancellationToken);

    public async Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Listing>(command);
    }

    public async Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetByDonorAsync(Guid donorId, ListingStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE DonorId = @DonorId AND IsDeleted = 0";
        var statusFilterSql = status is null ? string.Empty : " AND Status = @Status";
        var parameters = new { DonorId = donorId, Status = status, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var countCommand = new CommandDefinition("SELECT COUNT(*) FROM Listings" + whereSql + statusFilterSql, parameters, cancellationToken: cancellationToken);
        var totalCount = await connection.ExecuteScalarAsync<int>(countCommand);

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + statusFilterSql + " ORDER BY CreatedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<Listing>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ListingImage>> GetImagesAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        const string sql = "SELECT Id, ListingId, ImageUrl, CreatedAtUtc, UpdatedAtUtc FROM ListingImages WHERE ListingId = @ListingId ORDER BY CreatedAtUtc;";
        var command = new CommandDefinition(sql, new { ListingId = listingId }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ListingImage>(command)).ToList();
    }

    public async Task<IReadOnlyList<ListingTimelineEvent>> GetTimelineAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        const string sql = "SELECT Id, ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc FROM ListingTimeline WHERE ListingId = @ListingId ORDER BY CreatedAtUtc;";
        var command = new CommandDefinition(sql, new { ListingId = listingId }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ListingTimelineEvent>(command)).ToList();
    }

    public async Task UpdateAsync(Listing listing, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Listings SET
    Title = @Title,
    FoodType = @FoodType,
    DietType = @DietType,
    MealType = @MealType,
    QuantityMeals = @QuantityMeals,
    FreshnessTag = @FreshnessTag,
    PreparedAtUtc = @PreparedAtUtc,
    PickupDeadlineUtc = @PickupDeadlineUtc,
    PickupAddress = @PickupAddress,
    Latitude = @Latitude,
    Longitude = @Longitude,
    Location = geography::Point(@Latitude, @Longitude, 4326),
    UpdatedAtUtc = @UpdatedAtUtc
WHERE Id = @Id AND IsDeleted = 0;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, listing, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public Task ChangeStatusAsync(Listing listing, ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string updateSql = "UPDATE Listings SET Status = @Status, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @Id AND IsDeleted = 0;";
            const string insertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";

            await connection.ExecuteAsync(new CommandDefinition(updateSql, listing, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(insertTimelineSql, timelineEvent, transaction, cancellationToken: cancellationToken));
        }, cancellationToken);

    public async Task<Guid> AddImageAsync(ListingImage image, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO ListingImages (Id, ListingId, ImageUrl, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (NEWID(), @ListingId, @ImageUrl, @CreatedAtUtc, @UpdatedAtUtc);";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, image, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(command);
    }
}
