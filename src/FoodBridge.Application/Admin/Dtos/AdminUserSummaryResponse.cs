namespace FoodBridge.Application.Admin.Dtos;

/// <param name="RequiredDocumentTypes">What this role must submit; empty when none is needed.</param>
/// <param name="SubmittedDocumentTypes">What they've actually uploaded.</param>
/// <param name="IsReadyForReview">
/// Pending, and everything required is in — the accounts the admin can actually act on. Lets the
/// queue separate "waiting on me" from "waiting on them" without the client re-deriving the rule.
/// </param>
/// <param name="SelfieUrl">
/// The user's submitted selfie URL, or null. Lets the queue show a face thumbnail in place of the
/// initials avatar without a per-row fetch.
/// </param>
public sealed record AdminUserSummaryResponse(
    Guid Id,
    string Mobile,
    string Name,
    string Role,
    string AccountStatus,
    string? City,
    bool IsAvailable,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> RequiredDocumentTypes,
    IReadOnlyList<string> SubmittedDocumentTypes,
    bool IsReadyForReview,
    string? SelfieUrl);
