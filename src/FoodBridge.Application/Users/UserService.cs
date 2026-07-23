using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Application.Users.Dtos;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Application.Users;

public sealed class UserService : IUserService
{
    private const long MaxAvatarSizeBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedAvatarExtensions = { ".jpg", ".jpeg", ".png" };

    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public UserService(IUserRepository userRepository, IFileStorage fileStorage, ICurrentUser currentUser, IClock clock)
    {
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId != targetUserId && !_currentUser.IsInRole(nameof(UserRole.Admin)))
        {
            throw new UnauthorizedAccessException("You can only view your own profile.");
        }

        var user = await GetUserOrThrowAsync(targetUserId, cancellationToken);
        return Result.Success(user.ToProfileResponse());
    }

    public async Task<Result<UserProfileResponse>> UpdateProfileAsync(Guid targetUserId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSelf(targetUserId, "You can only edit your own profile.");

        var user = await GetUserOrThrowAsync(targetUserId, cancellationToken);

        user.Name = request.Name;
        user.City = request.City;
        user.Address = request.Address;
        user.Latitude = request.Latitude;
        user.Longitude = request.Longitude;
        user.CapacityMeals = user.Role == UserRole.Recipient ? request.CapacityMeals : null;
        user.UpdatedAtUtc = _clock.UtcNow;

        await _userRepository.UpdateProfileAsync(user, cancellationToken);
        return Result.Success(user.ToProfileResponse());
    }

    public async Task<Result<UserProfileResponse>> UpdateAvailabilityAsync(Guid targetUserId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSelf(targetUserId, "You can only change your own availability.");

        var user = await GetUserOrThrowAsync(targetUserId, cancellationToken);
        if (user.Role is not (UserRole.Volunteer or UserRole.Recipient))
        {
            throw new UnauthorizedAccessException("Only volunteers and recipients can toggle availability.");
        }

        await _userRepository.UpdateAvailabilityAsync(targetUserId, request.IsAvailable, cancellationToken);
        user.IsAvailable = request.IsAvailable;
        return Result.Success(user.ToProfileResponse());
    }

    public async Task<Result<AvatarUploadResponse>> UploadAvatarAsync(Guid targetUserId, Stream fileContent, string fileExtension, long fileSizeBytes, CancellationToken cancellationToken = default)
    {
        EnsureSelf(targetUserId, "You can only update your own avatar.");

        if (fileSizeBytes > MaxAvatarSizeBytes)
        {
            return Result.Failure<AvatarUploadResponse>("Avatar must be 2MB or smaller.");
        }

        if (!AllowedAvatarExtensions.Contains(fileExtension.ToLowerInvariant()))
        {
            return Result.Failure<AvatarUploadResponse>("Avatar must be a JPG or PNG image.");
        }

        await GetUserOrThrowAsync(targetUserId, cancellationToken);

        var avatarUrl = await _fileStorage.SaveAsync(fileContent, fileExtension.ToLowerInvariant(), cancellationToken);
        await _userRepository.UpdateAvatarUrlAsync(targetUserId, avatarUrl, cancellationToken);

        return Result.Success(new AvatarUploadResponse(avatarUrl));
    }

    private void EnsureSelf(Guid targetUserId, string message)
    {
        if (_currentUser.UserId != targetUserId)
        {
            throw new UnauthorizedAccessException(message);
        }
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User", userId);
        }

        return user;
    }
}
