using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Reports.Dtos;

namespace FoodBridge.Application.Reports;

public sealed class ReportService : IReportService
{
    private readonly IReportsReader _reportsReader;
    private readonly ICurrentUser _currentUser;

    public ReportService(IReportsReader reportsReader, ICurrentUser currentUser)
    {
        _reportsReader = reportsReader;
        _currentUser = currentUser;
    }

    public async Task<Result<DonorReportResponse>> GetDonorReportAsync(CancellationToken cancellationToken = default)
    {
        var donorId = _currentUser.UserId;
        var (totalListings, totalMealsDonated, totalCertificates) = await _reportsReader.GetDonorSummaryAsync(donorId, cancellationToken);
        var byMonth = await _reportsReader.GetDonorMealsByMonthAsync(donorId, cancellationToken);
        return Result.Success(new DonorReportResponse(totalListings, totalMealsDonated, totalCertificates, byMonth));
    }

    public async Task<Result<VolunteerReportResponse>> GetVolunteerReportAsync(CancellationToken cancellationToken = default)
    {
        var volunteerId = _currentUser.UserId;
        var (totalDeliveries, totalPoints) = await _reportsReader.GetVolunteerSummaryAsync(volunteerId, cancellationToken);
        var byMonth = await _reportsReader.GetVolunteerDeliveriesByMonthAsync(volunteerId, cancellationToken);
        return Result.Success(new VolunteerReportResponse(totalDeliveries, totalPoints, byMonth));
    }

    public async Task<Result<RecipientReportResponse>> GetRecipientReportAsync(CancellationToken cancellationToken = default)
    {
        var recipientId = _currentUser.UserId;
        var (totalMealsReceived, totalDeliveriesReceived) = await _reportsReader.GetRecipientSummaryAsync(recipientId, cancellationToken);
        var byMonth = await _reportsReader.GetRecipientMealsByMonthAsync(recipientId, cancellationToken);
        return Result.Success(new RecipientReportResponse(totalMealsReceived, totalDeliveriesReceived, byMonth));
    }

    public async Task<Result<PlatformReportResponse>> GetPlatformReportAsync(CancellationToken cancellationToken = default)
    {
        var (totalMealsDonated, totalDeliveries, totalCertificates, totalUsers) = await _reportsReader.GetPlatformSummaryAsync(cancellationToken);
        var byMonth = await _reportsReader.GetPlatformMealsByMonthAsync(cancellationToken);
        return Result.Success(new PlatformReportResponse(totalMealsDonated, totalDeliveries, totalCertificates, totalUsers, byMonth));
    }
}
