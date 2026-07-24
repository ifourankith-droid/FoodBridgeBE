using FoodBridge.Application.Common;
using FoodBridge.Application.Tracking.Dtos;

namespace FoodBridge.Application.Tracking;

public interface ITrackingService
{
    /// <summary>REST fallback for when the caller isn't connected to TrackingHub. Donor, assigned volunteer, or matched recipient only.</summary>
    Task<Result<TrackingResponse?>> GetTrackingAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called by TrackingHub as the assigned volunteer reports their position. Returns
    /// the stored reading (including its server-assigned timestamp) so the caller can
    /// broadcast the exact same value it persisted, rather than recomputing "now".
    /// </summary>
    Task<Result<TrackingResponse>> ReportLocationAsync(Guid listingId, decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
}
