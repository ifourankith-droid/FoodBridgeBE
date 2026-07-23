namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Read-only lookup used by <c>RecipientMatcher</c> — kept separate from
/// <see cref="IUserRepository"/> since it's a narrow, recipient-matching-specific
/// query, not a general user read/write concern.
/// </summary>
public interface IRecipientReader
{
    Task<Guid?> FindNearestAvailableRecipientIdAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
}
