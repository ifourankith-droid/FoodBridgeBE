using FoodBridge.Application.Common;
using FoodBridge.Application.Reports.Dtos;

namespace FoodBridge.Application.Reports;

public interface IReportService
{
    /// <summary>Caller's own donor impact report. Role restricted via [Authorize(Policy = "DonorOnly")] on the controller, not here.</summary>
    Task<Result<DonorReportResponse>> GetDonorReportAsync(CancellationToken cancellationToken = default);

    Task<Result<VolunteerReportResponse>> GetVolunteerReportAsync(CancellationToken cancellationToken = default);

    Task<Result<RecipientReportResponse>> GetRecipientReportAsync(CancellationToken cancellationToken = default);

    /// <summary>Platform-wide, Admin only — role restricted on the controller, not here.</summary>
    Task<Result<PlatformReportResponse>> GetPlatformReportAsync(CancellationToken cancellationToken = default);
}
