using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class ListingRepository : BaseRepository, IListingRepository
{
    private const string SelectSql = @"
SELECT Id, DonorId, Title, FoodType, DietType, MealType, QuantityMeals, FreshnessTag, PreparedAtUtc, PickupDeadlineUtc, PickupAddress, Latitude, Longitude, Status, VolunteerId, RecipientId, EstimatedPickupAtUtc, IsDeleted, CreatedAtUtc, UpdatedAtUtc
FROM Listings";

    public ListingRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public Task<Guid> CreateAsync(Listing listing, ListingTimelineEvent creationEvent, IReadOnlyList<Notification> volunteerNotifications, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string insertListingSql = @"
INSERT INTO Listings (DonorId, Title, FoodType, DietType, MealType, QuantityMeals, FreshnessTag, PreparedAtUtc, PickupDeadlineUtc, PickupAddress, Latitude, Longitude, Location, Status, VolunteerId, RecipientId, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@DonorId, @Title, @FoodType, @DietType, @MealType, @QuantityMeals, @FreshnessTag, @PreparedAtUtc, @PickupDeadlineUtc, @PickupAddress, @Latitude, @Longitude,
        " + GeoHelper.PointFromLatLngFragment + @",
        @Status, @VolunteerId, @RecipientId, @IsDeleted, @CreatedAtUtc, @UpdatedAtUtc);";

            var listingId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertListingSql, listing, transaction, cancellationToken: cancellationToken));
            listing.Id = listingId;
            creationEvent.ListingId = listingId;

            const string insertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";

            await connection.ExecuteAsync(new CommandDefinition(insertTimelineSql, creationEvent, transaction, cancellationToken: cancellationToken));

            const string insertNotificationSql = @"
INSERT INTO Notifications (UserId, Type, Title, Body, PayloadJson, IsRead, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@UserId, @Type, @Title, @Body, @PayloadJson, @IsRead, @CreatedAtUtc, @UpdatedAtUtc);";
            foreach (var notification in volunteerNotifications)
            {
                notification.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertNotificationSql, notification, transaction, cancellationToken: cancellationToken));
            }

            return listingId;
        }, cancellationToken);

    public async Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Listing>(command);
    }

    public async Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetByDonorAsync(Guid donorId, ListingStatus? status, DietType? dietType, MealType? mealType, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE DonorId = @DonorId AND IsDeleted = 0";
        var statusFilterSql = status is null ? string.Empty : " AND Status = @Status";
        var dietFilterSql = dietType is null ? string.Empty : " AND DietType = @DietType";
        var mealFilterSql = mealType is null ? string.Empty : " AND MealType = @MealType";
        var filterSql = statusFilterSql + dietFilterSql + mealFilterSql;
        var parameters = new { DonorId = donorId, Status = status, DietType = dietType, MealType = mealType, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var countCommand = new CommandDefinition("SELECT COUNT(*) FROM Listings" + whereSql + filterSql, parameters, cancellationToken: cancellationToken);
        var totalCount = await connection.ExecuteScalarAsync<int>(countCommand);

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + filterSql + " ORDER BY CreatedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
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
    Location = " + GeoHelper.PointFromLatLngFragment + @",
    UpdatedAtUtc = @UpdatedAtUtc
WHERE Id = @Id AND IsDeleted = 0;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, listing, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Updates status (plus VolunteerId/RecipientId, harmlessly re-set to their current
    /// values when unchanged by the caller) and inserts the timeline event atomically.
    /// Used by cancel, confirm-pickup (also assigns RecipientId), and confirm-delivery.
    /// </summary>
    public Task ChangeStatusAsync(Listing listing, ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string updateSql = "UPDATE Listings SET Status = @Status, VolunteerId = @VolunteerId, RecipientId = @RecipientId, EstimatedPickupAtUtc = @EstimatedPickupAtUtc, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @Id AND IsDeleted = 0;";
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

    /// <summary>
    /// Conditional UPDATE ... WHERE Status = Pending is the actual concurrency guard:
    /// exactly one of two racing claims affects a row, the loser gets rowsAffected == 0.
    /// </summary>
    public Task<bool> TryClaimAsync(Guid listingId, Guid volunteerId, DateTime? estimatedPickupAtUtc, ListingTimelineEvent claimEvent, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string updateSql = @"
UPDATE Listings SET Status = @ClaimedStatus, VolunteerId = @VolunteerId, EstimatedPickupAtUtc = @EstimatedPickupAtUtc, UpdatedAtUtc = @UpdatedAtUtc
WHERE Id = @ListingId AND Status = @PendingStatus AND IsDeleted = 0;";

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                updateSql,
                new
                {
                    ListingId = listingId,
                    VolunteerId = volunteerId,
                    EstimatedPickupAtUtc = estimatedPickupAtUtc,
                    UpdatedAtUtc = claimEvent.CreatedAtUtc,
                    ClaimedStatus = (byte)ListingStatus.Claimed,
                    PendingStatus = (byte)ListingStatus.Pending,
                },
                transaction,
                cancellationToken: cancellationToken));

            if (rowsAffected == 0)
            {
                return false;
            }

            claimEvent.ListingId = listingId;

            const string insertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";

            await connection.ExecuteAsync(new CommandDefinition(insertTimelineSql, claimEvent, transaction, cancellationToken: cancellationToken));
            return true;
        }, cancellationToken);

    public async Task<(IReadOnlyList<NearbyListing> Items, int TotalCount)> GetNearbyPendingAsync(decimal latitude, decimal longitude, double radiusMeters, DietType? dietType, MealType? mealType, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        var distanceSql = $"Location.STDistance({GeoHelper.PointFromLatLngFragment})";
        var dietFilterSql = dietType is null ? string.Empty : " AND DietType = @DietType";
        var mealFilterSql = mealType is null ? string.Empty : " AND MealType = @MealType";
        var whereSql = $@"
WHERE Status = @PendingStatus AND IsDeleted = 0 AND PickupDeadlineUtc > @NowUtc
    AND {distanceSql} <= @RadiusMeters{dietFilterSql}{mealFilterSql}";

        var parameters = new
        {
            Latitude = latitude,
            Longitude = longitude,
            RadiusMeters = radiusMeters,
            PendingStatus = (byte)ListingStatus.Pending,
            NowUtc = DateTime.UtcNow,
            DietType = dietType,
            MealType = mealType,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize,
        };

        var countCommand = new CommandDefinition("SELECT COUNT(*) FROM Listings" + whereSql, parameters, cancellationToken: cancellationToken);
        var totalCount = await connection.ExecuteScalarAsync<int>(countCommand);

        var itemsSql = $@"
SELECT Id, Title, FoodType, DietType, MealType, QuantityMeals, FreshnessTag, PickupDeadlineUtc, PickupAddress, Latitude, Longitude,
       {distanceSql} AS DistanceMeters
FROM Listings
{whereSql}
ORDER BY DistanceMeters ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var itemsCommand = new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<NearbyListing>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetIncomingForRecipientAsync(Guid recipientId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetByRecipientAndStatusAsync(recipientId, ListingStatus.PickedUp, page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetHistoryForRecipientAsync(Guid recipientId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetByRecipientAndStatusAsync(recipientId, ListingStatus.Confirmed, page, pageSize, cancellationToken);

    private async Task<(IReadOnlyList<Listing> Items, int TotalCount)> GetByRecipientAndStatusAsync(Guid recipientId, ListingStatus status, int page, int pageSize, CancellationToken cancellationToken)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE RecipientId = @RecipientId AND Status = @Status AND IsDeleted = 0";
        var parameters = new { RecipientId = recipientId, Status = (byte)status, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Listings" + whereSql, parameters, cancellationToken: cancellationToken));

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + " ORDER BY UpdatedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<Listing>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task AddTimelineEventAsync(ListingTimelineEvent timelineEvent, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";

        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, timelineEvent, cancellationToken: cancellationToken));
    }

    public Task ReassignRecipientAsync(Listing listing, ListingTimelineEvent timelineEvent, Notification? volunteerNotification, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string updateSql = "UPDATE Listings SET RecipientId = @RecipientId, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @Id AND IsDeleted = 0;";
            const string insertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";

            await connection.ExecuteAsync(new CommandDefinition(updateSql, listing, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(insertTimelineSql, timelineEvent, transaction, cancellationToken: cancellationToken));

            if (volunteerNotification is not null)
            {
                const string insertNotificationSql = @"
INSERT INTO Notifications (UserId, Type, Title, Body, PayloadJson, IsRead, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@UserId, @Type, @Title, @Body, @PayloadJson, @IsRead, @CreatedAtUtc, @UpdatedAtUtc);";
                volunteerNotification.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertNotificationSql, volunteerNotification, transaction, cancellationToken: cancellationToken));
            }
        }, cancellationToken);

    public Task ConfirmReceiptAsync(Listing listing, ListingTimelineEvent timelineEvent, VolunteerPoint volunteerPoint, Certificate certificate, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            const string updateListingSql = "UPDATE Listings SET Status = @Status, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @Id AND IsDeleted = 0;";
            await connection.ExecuteAsync(new CommandDefinition(updateListingSql, listing, transaction, cancellationToken: cancellationToken));

            const string insertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, @ActorUserId, @Note, @PhotoUrl, @CreatedAtUtc);";
            await connection.ExecuteAsync(new CommandDefinition(insertTimelineSql, timelineEvent, transaction, cancellationToken: cancellationToken));

            const string insertPointsSql = @"
INSERT INTO VolunteerPoints (VolunteerId, ListingId, Points, Reason, CreatedAtUtc, UpdatedAtUtc)
VALUES (@VolunteerId, @ListingId, @Points, @Reason, @CreatedAtUtc, @UpdatedAtUtc);";
            await connection.ExecuteAsync(new CommandDefinition(insertPointsSql, volunteerPoint, transaction, cancellationToken: cancellationToken));

            // Sequence number is a same-transaction COUNT, not a SQL Server SEQUENCE — a
            // simple choice with a small collision window under true concurrency; see the
            // decisions log.
            var monthPrefix = $"FB-{timelineEvent.CreatedAtUtc:yyyyMM}-";
            var countThisMonth = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Certificates WHERE CertificateNumber LIKE @Prefix + '%';",
                new { Prefix = monthPrefix },
                transaction,
                cancellationToken: cancellationToken));
            certificate.CertificateNumber = SlugHelper.BuildCertificateNumber(timelineEvent.CreatedAtUtc, countThisMonth + 1);

            const string insertCertificateSql = @"
INSERT INTO Certificates (CertificateNumber, DonorId, ListingId, MealsCount, IssuedAtUtc, PdfUrl, CreatedAtUtc, UpdatedAtUtc)
VALUES (@CertificateNumber, @DonorId, @ListingId, @MealsCount, @IssuedAtUtc, @PdfUrl, @CreatedAtUtc, @UpdatedAtUtc);";
            await connection.ExecuteAsync(new CommandDefinition(insertCertificateSql, certificate, transaction, cancellationToken: cancellationToken));

            const string insertNotificationSql = @"
INSERT INTO Notifications (UserId, Type, Title, Body, PayloadJson, IsRead, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@UserId, @Type, @Title, @Body, @PayloadJson, @IsRead, @CreatedAtUtc, @UpdatedAtUtc);";
            foreach (var notification in notifications)
            {
                notification.Id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertNotificationSql, notification, transaction, cancellationToken: cancellationToken));
            }
        }, cancellationToken);

    public Task<(IReadOnlyList<Guid> ExpiredIds, IReadOnlyList<Guid> RevertedToPendingIds)> ExpirePastDeadlineListingsAsync(DateTime nowUtc, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            // Step 1: a volunteer who claimed and never showed up must not leave perishable
            // food stuck forever — revert Claimed-past-deadline listings back to Pending.
            // Reuses the state machine's existing Claimed→Pending transition (see
            // ListingStateMachine); does not invent a new one.
            const string revertSql = @"
UPDATE Listings
SET Status = @PendingStatus, VolunteerId = NULL, UpdatedAtUtc = @NowUtc
OUTPUT INSERTED.Id
WHERE Status = @ClaimedStatus AND PickupDeadlineUtc <= @NowUtc AND IsDeleted = 0;";

            var revertedIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
                revertSql,
                new { PendingStatus = (byte)ListingStatus.Pending, ClaimedStatus = (byte)ListingStatus.Claimed, NowUtc = nowUtc },
                transaction,
                cancellationToken: cancellationToken))).ToList();

            if (revertedIds.Count > 0)
            {
                const string insertRevertTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, NULL, @Note, NULL, @CreatedAtUtc);";

                var revertRows = revertedIds.Select(id => new
                {
                    ListingId = id,
                    FromStatus = (byte)ListingStatus.Claimed,
                    ToStatus = (byte)ListingStatus.Pending,
                    Note = "Volunteer did not act before the pickup deadline — automatically returned to Pending.",
                    CreatedAtUtc = nowUtc,
                });

                await connection.ExecuteAsync(new CommandDefinition(insertRevertTimelineSql, revertRows, transaction, cancellationToken: cancellationToken));
            }

            // Step 2: expire every Pending listing whose deadline has passed — including
            // rows just reverted in step 1 above, since their deadline is by definition
            // already gone too, so there's no reason to give them a further Pending window.
            const string expireSql = @"
UPDATE Listings
SET Status = @ExpiredStatus, UpdatedAtUtc = @NowUtc
OUTPUT INSERTED.Id
WHERE Status = @PendingStatus AND PickupDeadlineUtc <= @NowUtc AND IsDeleted = 0;";

            var expiredIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
                expireSql,
                new { ExpiredStatus = (byte)ListingStatus.Expired, PendingStatus = (byte)ListingStatus.Pending, NowUtc = nowUtc },
                transaction,
                cancellationToken: cancellationToken))).ToList();

            if (expiredIds.Count > 0)
            {
                const string insertExpireTimelineSql = @"
INSERT INTO ListingTimeline (ListingId, FromStatus, ToStatus, ActorUserId, Note, PhotoUrl, CreatedAtUtc)
VALUES (@ListingId, @FromStatus, @ToStatus, NULL, @Note, NULL, @CreatedAtUtc);";

                var expireRows = expiredIds.Select(id => new
                {
                    ListingId = id,
                    FromStatus = (byte)ListingStatus.Pending,
                    ToStatus = (byte)ListingStatus.Expired,
                    Note = "Listing expired automatically (pickup deadline passed).",
                    CreatedAtUtc = nowUtc,
                });

                await connection.ExecuteAsync(new CommandDefinition(insertExpireTimelineSql, expireRows, transaction, cancellationToken: cancellationToken));
            }

            return ((IReadOnlyList<Guid>)expiredIds, (IReadOnlyList<Guid>)revertedIds);
        }, cancellationToken);
}
