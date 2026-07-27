using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.DropOffLocations;

public sealed class DropOffLocationService : IDropOffLocationService
{
    private readonly IDropOffLocationRepository _dropOffLocationRepository;
    private readonly IClock _clock;

    public DropOffLocationService(IDropOffLocationRepository dropOffLocationRepository, IClock clock)
    {
        _dropOffLocationRepository = dropOffLocationRepository;
        _clock = clock;
    }

    public async Task<Result<DropOffLocationResponse>> CreateAsync(CreateDropOffLocationRequest request, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var location = new DropOffLocation
        {
            Name = request.Name,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            City = request.City,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        location.Id = await _dropOffLocationRepository.CreateAsync(location, cancellationToken);

        return Result.Success(location.ToResponse(), "Drop-off location created successfully.");
    }

    public async Task<Result<PagedResult<DropOffLocationResponse>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _dropOffLocationRepository.GetAllAsync(normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<DropOffLocationResponse>(items.Select(l => l.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public Task<Result<DropOffLocationResponse>> ActivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, true, "Drop-off location activated successfully.", cancellationToken);

    public Task<Result<DropOffLocationResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, false, "Drop-off location deactivated successfully.", cancellationToken);

    private async Task<Result<DropOffLocationResponse>> SetActiveAsync(Guid id, bool isActive, string successMessage, CancellationToken cancellationToken)
    {
        var location = await _dropOffLocationRepository.GetByIdAsync(id, cancellationToken);
        if (location is null)
        {
            throw new NotFoundException("DropOffLocation", id);
        }

        await _dropOffLocationRepository.SetActiveAsync(id, isActive, cancellationToken);
        location.IsActive = isActive;

        return Result.Success(location.ToResponse(), successMessage);
    }
}
