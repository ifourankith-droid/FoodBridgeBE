using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.DonorAddresses.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.DonorAddresses;

public sealed class DonorAddressService : IDonorAddressService
{
    private readonly IDonorAddressRepository _donorAddressRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DonorAddressService(IDonorAddressRepository donorAddressRepository, ICurrentUser currentUser, IClock clock)
    {
        _donorAddressRepository = donorAddressRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<DonorAddressResponse>> CreateAsync(CreateDonorAddressRequest request, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var address = new DonorAddress
        {
            DonorId = _currentUser.UserId,
            Label = request.Label,
            Address = request.Address,
            City = Normalize(request.City),
            State = Normalize(request.State),
            Pincode = Normalize(request.Pincode),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsDefault = request.IsDefault,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        address.Id = await _donorAddressRepository.CreateAsync(address, cancellationToken);

        if (request.IsDefault)
        {
            await _donorAddressRepository.ClearDefaultAsync(_currentUser.UserId, address.Id, cancellationToken);
        }

        return Result.Success(address.ToResponse(), "Address saved successfully.");
    }

    public async Task<Result<PagedResult<DonorAddressResponse>>> GetMyAddressesAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var (items, totalCount) = await _donorAddressRepository.GetByDonorAsync(_currentUser.UserId, normalizedPage, normalizedPageSize, cancellationToken);
        return Result.Success(new PagedResult<DonorAddressResponse>(items.Select(a => a.ToResponse()).ToList(), totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<DonorAddressResponse>> GetByIdAsync(Guid addressId, CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedAddressOrThrowAsync(addressId, cancellationToken);
        return Result.Success(address.ToResponse());
    }

    public async Task<Result<DonorAddressResponse>> UpdateAsync(Guid addressId, UpdateDonorAddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedAddressOrThrowAsync(addressId, cancellationToken);

        address.Label = request.Label;
        address.Address = request.Address;
        address.City = Normalize(request.City);
        address.State = Normalize(request.State);
        address.Pincode = Normalize(request.Pincode);
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
        address.IsDefault = request.IsDefault;
        address.UpdatedAtUtc = _clock.UtcNow;

        await _donorAddressRepository.UpdateAsync(address, cancellationToken);

        if (request.IsDefault)
        {
            await _donorAddressRepository.ClearDefaultAsync(_currentUser.UserId, address.Id, cancellationToken);
        }

        return Result.Success(address.ToResponse(), "Address updated successfully.");
    }

    public async Task<Result> DeleteAsync(Guid addressId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAddressOrThrowAsync(addressId, cancellationToken);
        await _donorAddressRepository.DeleteAsync(addressId, cancellationToken);
        return Result.Success("Address deleted successfully.");
    }

    /// <summary>
    /// Blank → null, so an untouched optional input doesn't store an empty string that then has to be
    /// special-cased everywhere a caller asks "does this address have a city?".
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<DonorAddress> GetOwnedAddressOrThrowAsync(Guid addressId, CancellationToken cancellationToken)
    {
        var address = await _donorAddressRepository.GetByIdAsync(addressId, cancellationToken);
        if (address is null)
        {
            throw new NotFoundException("DonorAddress", addressId);
        }

        if (address.DonorId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException("You can only access your own saved addresses.");
        }

        return address;
    }
}
