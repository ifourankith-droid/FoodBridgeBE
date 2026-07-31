using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.DropOffLocations.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace FoodBridge.Application.DropOffLocations;

public sealed class DropOffLocationService : IDropOffLocationService
{
    private readonly IDropOffLocationRepository _dropOffLocationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IClock _clock;
    private readonly DropOffSettings _settings;

    public DropOffLocationService(IDropOffLocationRepository dropOffLocationRepository, IUserRepository userRepository, IClock clock, IOptions<DropOffSettings> settings)
    {
        _dropOffLocationRepository = dropOffLocationRepository;
        _userRepository = userRepository;
        _clock = clock;
        _settings = settings.Value;
    }

    public async Task<Result<PagedResult<DropOffHotspotResponse>>> GetHotspotsAsync(decimal latitude, decimal longitude, double? radiusKm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90)
        {
            return Result.Failure<PagedResult<DropOffHotspotResponse>>("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            return Result.Failure<PagedResult<DropOffHotspotResponse>>("Longitude must be between -180 and 180.");
        }

        // Same clamp-don't-reject approach the volunteer's nearby-listings query already uses for
        // its radius, so an over-large value degrades to the maximum rather than erroring.
        var effectiveRadiusKm = radiusKm switch
        {
            null or <= 0 => _settings.HotspotRadiusKm,
            var value when value > _settings.MaxHotspotRadiusKm => _settings.MaxHotspotRadiusKm,
            var value => value.Value,
        };

        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);

        var (items, totalCount) = await _dropOffLocationRepository.GetHotspotsAsync(
            latitude,
            longitude,
            effectiveRadiusKm * 1000,
            _clock.UtcNow,
            TimeSpan.FromHours(_settings.CooldownHours),
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        // Resolve the adding volunteer's name for field-discovered spots (cached per user).
        var names = new Dictionary<Guid, string?>();
        foreach (var id in items
            .Where(h => h.Location.CreatedByUserId.HasValue)
            .Select(h => h.Location.CreatedByUserId!.Value)
            .Distinct())
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            names[id] = user?.Name;
        }

        var responses = items
            .Select(h => h.ToResponse(
                h.Location.CreatedByUserId is Guid cid && names.TryGetValue(cid, out var name) ? name : null))
            .ToList();

        return Result.Success(new PagedResult<DropOffHotspotResponse>(
            responses,
            totalCount,
            normalizedPage,
            normalizedPageSize));
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
            Source = DropOffLocationSource.Admin,
            CreatedByUserId = null,
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
