using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations.Dtos;

namespace FoodBridge.Application.DropOffLocations;

public interface IDropOffLocationService
{
    Task<Result<DropOffLocationResponse>> CreateAsync(CreateDropOffLocationRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<DropOffLocationResponse>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<DropOffLocationResponse>> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DropOffLocationResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nearby drop-off spots with usage intensity and cooldown state, for the volunteer's hotspot
    /// map. Available spots come first, then nearest — so the head of the list is where they should
    /// take the food they're carrying.
    /// </summary>
    Task<Result<PagedResult<DropOffHotspotResponse>>> GetHotspotsAsync(decimal latitude, decimal longitude, double? radiusKm, int page, int pageSize, CancellationToken cancellationToken = default);
}
