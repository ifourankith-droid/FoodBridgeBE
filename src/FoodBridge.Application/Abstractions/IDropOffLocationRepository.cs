using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IDropOffLocationRepository
{
    Task<Guid> CreateAsync(DropOffLocation location, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DropOffLocation> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DropOffLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Nearest active location to the given point, or null if none are active. Used to suggest where a volunteer should take food when no recipient is available.</summary>
    Task<DropOffLocation?> GetNearestActiveAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
}
