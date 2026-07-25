using FoodBridge.Application.Common;
using FoodBridge.Application.Disputes.Dtos;

namespace FoodBridge.Application.Disputes;

public interface IDisputeService
{
    /// <summary>Raised by the donor, assigned volunteer, or matched recipient of the listing — anyone else gets 403.</summary>
    Task<Result<DisputeResponse>> CreateAsync(CreateDisputeRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<DisputeResponse>>> GetAllAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<DisputeResponse>> ResolveAsync(Guid disputeId, ResolveDisputeRequest request, CancellationToken cancellationToken = default);
}
