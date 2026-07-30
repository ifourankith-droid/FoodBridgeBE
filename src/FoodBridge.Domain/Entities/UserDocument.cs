using FoodBridge.Domain.Enums;

namespace FoodBridge.Domain.Entities;

/// <summary>
/// One piece of verification evidence uploaded by a user. Re-uploading the same
/// <see cref="Type"/> replaces the row (a volunteer who photographed their ID badly needs to be
/// able to try again), so <see cref="UpdatedAtUtc"/> is meaningful here unlike the append-only logs.
/// </summary>
public sealed class UserDocument
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserDocumentType Type { get; set; }

    /// <summary>Served from <c>/uploads</c> by the static-file middleware, like avatars.</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>The uploader's own filename, shown to the reviewing admin as a weak sanity signal.</summary>
    public string? OriginalFileName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
