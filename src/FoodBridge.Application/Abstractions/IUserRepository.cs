using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByMobileAsync(string mobile, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateProfileAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken = default);

    Task UpdateAvatarUrlAsync(Guid id, string avatarUrl, CancellationToken cancellationToken = default);
}
