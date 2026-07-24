using FoodBridge.Application.Common;
using FoodBridge.Application.Disputes.Dtos;

namespace FoodBridge.Application.Disputes;

public interface IDisputeService
{
    Task<Result<PagedResult<DisputeResponse>>> GetAllAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<DisputeResponse>> ResolveAsync(Guid disputeId, ResolveDisputeRequest request, CancellationToken cancellationToken = default);
}
