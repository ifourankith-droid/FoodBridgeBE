using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

public interface IDisputeRepository
{
    Task<(IReadOnlyList<Dispute> Items, int TotalCount)> GetAllAsync(DisputeStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Dispute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task ResolveAsync(Guid id, Guid resolvedByUserId, string resolutionNote, CancellationToken cancellationToken = default);
}
