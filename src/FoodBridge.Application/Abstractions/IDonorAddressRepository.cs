using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IDonorAddressRepository
{
    Task<Guid> CreateAsync(DonorAddress address, CancellationToken cancellationToken = default);

    Task<DonorAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DonorAddress> Items, int TotalCount)> GetByDonorAsync(Guid donorId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// The donor's default address — the one <c>GET /auth/me</c> reports, and the only address that
    /// carries a label. Null when they have saved none. Falls back to their most recent address if no
    /// row is flagged default, since nothing in the schema guarantees one exists.
    /// </summary>
    Task<DonorAddress?> GetDefaultAsync(Guid donorId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DonorAddress address, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Clears IsDefault on every other address owned by this donor — used when a new one is marked default.</summary>
    Task ClearDefaultAsync(Guid donorId, Guid exceptAddressId, CancellationToken cancellationToken = default);
}
