using FoodBridge.Application.Common;
using FoodBridge.Application.DonorAddresses.Dtos;

namespace FoodBridge.Application.DonorAddresses;

/// <summary>A donor's own saved address book. Self only throughout — enforced via ICurrentUser, not a policy.</summary>
public interface IDonorAddressService
{
    Task<Result<DonorAddressResponse>> CreateAsync(CreateDonorAddressRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<DonorAddressResponse>>> GetMyAddressesAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<DonorAddressResponse>> GetByIdAsync(Guid addressId, CancellationToken cancellationToken = default);

    Task<Result<DonorAddressResponse>> UpdateAsync(Guid addressId, UpdateDonorAddressRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid addressId, CancellationToken cancellationToken = default);
}
