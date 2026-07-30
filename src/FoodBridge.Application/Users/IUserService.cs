using FoodBridge.Application.Common;
using FoodBridge.Application.Users.Dtos;

namespace FoodBridge.Application.Users;

public interface IUserService
{
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid targetUserId, CancellationToken cancellationToken = default);

    Task<Result<UserProfileResponse>> UpdateProfileAsync(Guid targetUserId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserProfileResponse>> UpdateAvailabilityAsync(Guid targetUserId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<Result<AvatarUploadResponse>> UploadAvatarAsync(Guid targetUserId, Stream fileContent, string fileExtension, long fileSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads (or replaces) one verification document. Self only — nobody, admin included, submits
    /// evidence on another person's behalf, since the whole point is that it came from them.
    /// </summary>
    Task<Result<UserVerificationResponse>> UploadDocumentAsync(Guid targetUserId, string documentType, Stream fileContent, string fileExtension, long fileSizeBytes, string? originalFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verification state and submitted documents. Self or admin — the volunteer checks their own
    /// progress, the admin reads the same payload to review it.
    /// </summary>
    Task<Result<UserVerificationResponse>> GetVerificationAsync(Guid targetUserId, CancellationToken cancellationToken = default);
}
