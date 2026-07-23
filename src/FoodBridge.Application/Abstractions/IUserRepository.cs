using FoodBridge.Domain.Entities;

namespace FoodBridge.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByMobileAsync(string mobile, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(User user, CancellationToken cancellationToken = default);
}
