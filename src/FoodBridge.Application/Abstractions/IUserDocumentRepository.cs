using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>One submitted document, reduced to what the accounts browse needs: its type and servable URL.</summary>
public sealed record UserDocumentRef(UserDocumentType Type, string FileUrl);

/// <summary>
/// Verification documents. A narrow interface of its own rather than methods on
/// <see cref="IUserRepository"/> — same ISP reasoning already applied to <c>IRecipientReader</c> and
/// <c>ILeaderboardReader</c>: this is a separate concern that happens to hang off a user.
/// </summary>
public interface IUserDocumentRepository
{
    /// <summary>
    /// Inserts the document, or replaces the user's existing one of the same type. Returns the
    /// previous file's URL when one was replaced, so the caller can delete the orphaned file.
    /// </summary>
    Task<string?> UpsertAsync(UserDocument document, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDocument>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of the given users have submitted which documents (type + URL). Lets the admin accounts
    /// browse show a "documents submitted" indicator — and a selfie thumbnail — for a whole page in
    /// one query instead of one call per row.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<UserDocumentRef>>> GetDocumentsForUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
