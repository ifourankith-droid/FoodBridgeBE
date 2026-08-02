using FoodBridge.Domain.Enums;

namespace FoodBridge.Application.Abstractions;

/// <summary>Repository projection for the admin accounts browse.</summary>
public sealed class AdminUserSummary
{
    public Guid Id { get; set; }
    public string Mobile { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public AccountStatus AccountStatus { get; set; }
    public string? City { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Verification evidence this user has uploaded. Populated by <c>AdminService</c> in one batched
    /// query per page (not per row), so the queue can sort actionable accounts to the top without an
    /// N+1. Empty for roles that need no documents.
    /// </summary>
    public IReadOnlyList<UserDocumentType> SubmittedDocumentTypes { get; set; } = Array.Empty<UserDocumentType>();

    /// <summary>
    /// The user's submitted selfie URL, if any — set from the same batched query as
    /// <see cref="SubmittedDocumentTypes"/> so the queue can show a face thumbnail without a per-row
    /// fetch. Null when no selfie has been uploaded.
    /// </summary>
    public string? SelfieUrl { get; set; }
}
