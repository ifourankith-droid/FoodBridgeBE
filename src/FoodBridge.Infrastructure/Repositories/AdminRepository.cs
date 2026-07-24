using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class AdminRepository : BaseRepository, IAdminRepository
{
    public AdminRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<AdminDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    (SELECT COUNT(*) FROM Users WHERE Role = @DonorRole AND IsDeleted = 0) AS TotalDonors,
    (SELECT COUNT(*) FROM Users WHERE Role = @VolunteerRole AND IsDeleted = 0) AS TotalVolunteers,
    (SELECT COUNT(*) FROM Users WHERE Role = @RecipientRole AND IsDeleted = 0) AS TotalRecipients,
    (SELECT COUNT(*) FROM Users WHERE Role = @RecipientRole AND AccountStatus = @PendingStatus AND IsDeleted = 0) AS PendingRecipients,
    (SELECT COUNT(*) FROM Listings WHERE IsDeleted = 0) AS TotalListings,
    (SELECT COUNT(*) FROM Listings WHERE Status = @ListingPending AND IsDeleted = 0) AS PendingListings,
    (SELECT COUNT(*) FROM Listings WHERE Status IN @InFlightStatuses AND IsDeleted = 0) AS ActiveListings,
    (SELECT COUNT(*) FROM Listings WHERE Status = @ListingConfirmed AND IsDeleted = 0) AS ConfirmedListings,
    (SELECT COALESCE(SUM(MealsCount), 0) FROM Certificates) AS TotalMealsDonated,
    (SELECT COUNT(*) FROM Certificates) AS TotalCertificatesIssued,
    (SELECT COALESCE(SUM(Points), 0) FROM VolunteerPoints) AS TotalVolunteerPointsAwarded,
    (SELECT COUNT(*) FROM Disputes WHERE Status = @DisputeOpen) AS OpenDisputes,
    (SELECT COUNT(*) FROM Disputes WHERE Status = @DisputeResolved) AS ResolvedDisputes;";

        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                DonorRole = (byte)UserRole.Donor,
                VolunteerRole = (byte)UserRole.Volunteer,
                RecipientRole = (byte)UserRole.Recipient,
                PendingStatus = (byte)AccountStatus.Pending,
                ListingPending = (byte)ListingStatus.Pending,
                // int[], not byte[] — Dapper treats byte[] as a single varbinary value, not a
                // list to expand into IN (...), so an IN clause needs a different element type.
                InFlightStatuses = new[] { (int)ListingStatus.Claimed, (int)ListingStatus.PickedUp, (int)ListingStatus.Delivered },
                ListingConfirmed = (byte)ListingStatus.Confirmed,
                DisputeOpen = (byte)DisputeStatus.Open,
                DisputeResolved = (byte)DisputeStatus.Resolved,
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<AdminDashboardStats>(command);
    }

    public async Task<(IReadOnlyList<AdminListingSummary> Items, int TotalCount)> GetAllListingsAsync(ListingStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE l.IsDeleted = 0";
        var statusFilterSql = status is null ? string.Empty : " AND l.Status = @Status";
        var parameters = new { Status = status, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Listings l" + whereSql + statusFilterSql, parameters, cancellationToken: cancellationToken));

        var itemsSql = @"
SELECT l.Id, l.Title, l.Status, l.DonorId, u.Name AS DonorName, l.VolunteerId, l.RecipientId, l.QuantityMeals, l.PickupDeadlineUtc, l.CreatedAtUtc
FROM Listings l
JOIN Users u ON u.Id = l.DonorId" + whereSql + statusFilterSql + @"
ORDER BY l.CreatedAtUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var items = (await connection.QueryAsync<AdminListingSummary>(new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken))).ToList();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<AdminUserSummary> Items, int TotalCount)> GetAllUsersAsync(UserRole? role, AccountStatus? accountStatus, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE IsDeleted = 0";
        var roleFilterSql = role is null ? string.Empty : " AND Role = @Role";
        var statusFilterSql = accountStatus is null ? string.Empty : " AND AccountStatus = @AccountStatus";
        var parameters = new { Role = role, AccountStatus = accountStatus, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Users" + whereSql + roleFilterSql + statusFilterSql, parameters, cancellationToken: cancellationToken));

        var itemsSql = @"
SELECT Id, Mobile, Name, Role, AccountStatus, City, IsAvailable, CreatedAtUtc
FROM Users" + whereSql + roleFilterSql + statusFilterSql + @"
ORDER BY CreatedAtUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var items = (await connection.QueryAsync<AdminUserSummary>(new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken))).ToList();

        return (items, totalCount);
    }
}
