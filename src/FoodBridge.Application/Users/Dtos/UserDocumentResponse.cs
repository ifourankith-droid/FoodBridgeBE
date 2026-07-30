namespace FoodBridge.Application.Users.Dtos;

/// <param name="Type">"IdProof" or "Selfie".</param>
/// <param name="FileUrl">Servable URL under <c>/uploads</c>, same as avatars.</param>
public sealed record UserDocumentResponse(
    Guid Id,
    string Type,
    string FileUrl,
    string? OriginalFileName,
    DateTime UploadedAtUtc);

/// <summary>
/// A user's verification state and what they've submitted — the payload behind both the
/// volunteer's own "am I verified yet?" screen and the admin's review panel.
/// </summary>
/// <param name="AccountStatus">"Pending", "Verified" or "Suspended".</param>
/// <param name="RequiredDocumentTypes">
/// What this role must submit. Empty for roles that need no documents, so the client can render
/// "nothing needed" without hard-coding which roles those are.
/// </param>
/// <param name="MissingDocumentTypes">Of the required set, what hasn't been uploaded yet.</param>
/// <param name="IsReadyForReview">
/// Everything required is present and the account is still Pending — i.e. it's now the admin's move,
/// not the volunteer's. Computed server-side so the volunteer's screen and the admin's queue can
/// never disagree about whose turn it is.
/// </param>
public sealed record UserVerificationResponse(
    Guid UserId,
    string Role,
    string AccountStatus,
    IReadOnlyList<UserDocumentResponse> Documents,
    IReadOnlyList<string> RequiredDocumentTypes,
    IReadOnlyList<string> MissingDocumentTypes,
    bool IsReadyForReview);
