using FoodBridge.Application.Common;
using FoodBridge.Application.Users.Dtos;

namespace FoodBridge.Application.Users;

public interface IUserService
{
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid targetUserId, CancellationToken cancellationToken = default);

    Task<Result<UserProfileResponse>> UpdateProfileAsync(Guid targetUserId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserProfileResponse>> UpdateAvailabilityAsync(Guid targetUserId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<Result<AvatarUploadResponse>> UploadAvatarAsync(Guid targetUserId, Stream fileContent, string fileExtension, long fileSizeBytes, CancellationToken cancellationToken = default);
}
