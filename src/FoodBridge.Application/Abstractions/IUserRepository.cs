using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByMobileAsync(string mobile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the user and, when <paramref name="homeAddress"/> is supplied, their first saved
    /// address — in one transaction, so a registration can never half-succeed and leave a donor
    /// with an account but no address book. Mutates both entities' Ids with the generated values.
    /// </summary>
    Task<Guid> CreateAsync(User user, DonorAddress? homeAddress = null, CancellationToken cancellationToken = default);

    Task UpdateProfileAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken = default);

    Task UpdateAvatarUrlAsync(Guid id, string avatarUrl, CancellationToken cancellationToken = default);

    /// <summary>Admin-only write (verify/suspend) — the restriction lives in the calling service, not here.</summary>
    /// <summary>
    /// Updates the account's status and, when <paramref name="notification"/> is supplied, inserts it
    /// in the same transaction — so the user can never be told they were verified by a change that
    /// then rolled back.
    /// </summary>
    Task UpdateAccountStatusAsync(Guid id, AccountStatus accountStatus, Notification? notification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of available, Verified volunteers within <paramref name="radiusMeters"/> of the
    /// point — used to target the real-time "new listing nearby" push on listing creation.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetNearbyAvailableVolunteerIdsAsync(decimal latitude, decimal longitude, double radiusMeters, CancellationToken cancellationToken = default);
}
