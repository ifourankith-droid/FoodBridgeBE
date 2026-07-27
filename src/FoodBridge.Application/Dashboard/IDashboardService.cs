using FoodBridge.Application.Common;
using FoodBridge.Application.Dashboard.Dtos;

namespace FoodBridge.Application.Dashboard;

public interface IDashboardService
{
    /// <summary>latitude/longitude are optional — when omitted, falls back to the donor's own registered profile location for the "nearby recipients" widget (empty list if neither is available).</summary>
    Task<Result<DonorDashboardResponse>> GetDonorDashboardAsync(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default);

    /// <summary>latitude/longitude are optional — when omitted, falls back to the volunteer's own registered profile location for the "open listings nearby" widget (empty list if neither is available).</summary>
    Task<Result<VolunteerDashboardResponse>> GetVolunteerDashboardAsync(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default);

    Task<Result<RecipientDashboardResponse>> GetRecipientDashboardAsync(CancellationToken cancellationToken = default);
}
