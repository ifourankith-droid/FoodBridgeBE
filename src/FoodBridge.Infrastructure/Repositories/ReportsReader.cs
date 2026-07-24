using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Reports.Dtos;
using FoodBridge.Domain.Enums;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class ReportsReader : BaseRepository, IReportsReader
{
    public ReportsReader(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<(int TotalListings, int TotalMealsDonated, int TotalCertificates)> GetDonorSummaryAsync(Guid donorId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var parameters = new { DonorId = donorId };

        var totalListings = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Listings WHERE DonorId = @DonorId AND IsDeleted = 0;", parameters, cancellationToken: cancellationToken));
        var totalCertificates = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Certificates WHERE DonorId = @DonorId;", parameters, cancellationToken: cancellationToken));
        var totalMealsDonated = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(SUM(MealsCount), 0) FROM Certificates WHERE DonorId = @DonorId;", parameters, cancellationToken: cancellationToken));

        return (totalListings, totalMealsDonated, totalCertificates);
    }

    public async Task<IReadOnlyList<ChartPoint>> GetDonorMealsByMonthAsync(Guid donorId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        const string sql = @"
SELECT FORMAT(IssuedAtUtc, 'yyyy-MM') AS Period, SUM(MealsCount) AS Value
FROM Certificates
WHERE DonorId = @DonorId
GROUP BY FORMAT(IssuedAtUtc, 'yyyy-MM')
ORDER BY Period;";
        var command = new CommandDefinition(sql, new { DonorId = donorId }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ChartPoint>(command)).ToList();
    }

    public async Task<(int TotalDeliveries, int TotalPoints)> GetVolunteerSummaryAsync(Guid volunteerId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var parameters = new { VolunteerId = volunteerId };

        var totalDeliveries = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM VolunteerPoints WHERE VolunteerId = @VolunteerId;", parameters, cancellationToken: cancellationToken));
        var totalPoints = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(SUM(Points), 0) FROM VolunteerPoints WHERE VolunteerId = @VolunteerId;", parameters, cancellationToken: cancellationToken));

        return (totalDeliveries, totalPoints);
    }

    public async Task<IReadOnlyList<ChartPoint>> GetVolunteerDeliveriesByMonthAsync(Guid volunteerId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        const string sql = @"
SELECT FORMAT(CreatedAtUtc, 'yyyy-MM') AS Period, COUNT(*) AS Value
FROM VolunteerPoints
WHERE VolunteerId = @VolunteerId
GROUP BY FORMAT(CreatedAtUtc, 'yyyy-MM')
ORDER BY Period;";
        var command = new CommandDefinition(sql, new { VolunteerId = volunteerId }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ChartPoint>(command)).ToList();
    }

    public async Task<(int TotalMealsReceived, int TotalDeliveriesReceived)> GetRecipientSummaryAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var parameters = new { RecipientId = recipientId, ConfirmedStatus = (byte)ListingStatus.Confirmed };

        var totalDeliveriesReceived = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Listings WHERE RecipientId = @RecipientId AND Status = @ConfirmedStatus AND IsDeleted = 0;", parameters, cancellationToken: cancellationToken));
        var totalMealsReceived = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(SUM(QuantityMeals), 0) FROM Listings WHERE RecipientId = @RecipientId AND Status = @ConfirmedStatus AND IsDeleted = 0;", parameters, cancellationToken: cancellationToken));

        return (totalMealsReceived, totalDeliveriesReceived);
    }

    public async Task<IReadOnlyList<ChartPoint>> GetRecipientMealsByMonthAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        const string sql = @"
SELECT FORMAT(UpdatedAtUtc, 'yyyy-MM') AS Period, SUM(QuantityMeals) AS Value
FROM Listings
WHERE RecipientId = @RecipientId AND Status = @ConfirmedStatus AND IsDeleted = 0
GROUP BY FORMAT(UpdatedAtUtc, 'yyyy-MM')
ORDER BY Period;";
        var command = new CommandDefinition(sql, new { RecipientId = recipientId, ConfirmedStatus = (byte)ListingStatus.Confirmed }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ChartPoint>(command)).ToList();
    }
}
