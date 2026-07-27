using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations.Dtos;

namespace FoodBridge.Application.DropOffLocations;

public interface IDropOffLocationService
{
    Task<Result<DropOffLocationResponse>> CreateAsync(CreateDropOffLocationRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<DropOffLocationResponse>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<DropOffLocationResponse>> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DropOffLocationResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
