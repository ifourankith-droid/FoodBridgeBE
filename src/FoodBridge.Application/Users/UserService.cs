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

    /// <summary>
    /// 5MB, and PDF allowed alongside images — a phone photo of an ID is bigger than an avatar,
    /// and a scanned ID is very often a PDF. Same ceiling as listing/delivery photos.
    /// </summary>
    private const long MaxDocumentSizeBytes = 5 * 1024 * 1024;

    private readonly IUserRepository _userRepository;
    private readonly IUserDocumentRepository _userDocumentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public UserService(
        IUserRepository userRepository,
        IUserDocumentRepository userDocumentRepository,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        IClock clock)
    {
        _userRepository = userRepository;
        _userDocumentRepository = userDocumentRepository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<UserVerificationResponse>> UploadDocumentAsync(
        Guid targetUserId,
        string documentType,
        Stream fileContent,
        string fileExtension,
        long fileSizeBytes,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        EnsureSelf(targetUserId, "You can only upload your own verification documents.");

        if (!Enum.TryParse<UserDocumentType>(documentType, true, out var parsedType))
        {
            return Result.Failure<UserVerificationResponse>($"Unknown document type '{documentType}'. Expected IdProof or Selfie.");
        }

        var user = await GetUserOrThrowAsync(targetUserId, cancellationToken);

        var required = VerificationPolicy.RequiredDocuments(user.Role);
        if (!required.Contains(parsedType))
        {
            return Result.Failure<UserVerificationResponse>($"A {user.Role} account does not need a {parsedType} document.");
        }

        if (fileSizeBytes > MaxDocumentSizeBytes)
        {
            return Result.Failure<UserVerificationResponse>("Document must be 5MB or smaller.");
        }

        var extension = fileExtension.ToLowerInvariant();
        if (!ImageFileTypes.IsImageOrPdf(extension))
        {
            return Result.Failure<UserVerificationResponse>($"Document must be {ImageFileTypes.DocumentDescription}.");
        }

        // A selfie is the one piece of evidence that must actually be a photo — accepting a PDF
        // there would defeat the point of comparing a face against the ID.
        if (parsedType == UserDocumentType.Selfie && !ImageFileTypes.IsImage(extension))
        {
            return Result.Failure<UserVerificationResponse>($"Your selfie must be {ImageFileTypes.ImageDescription}.");
        }

        var now = _clock.UtcNow;
        var fileUrl = await _fileStorage.SaveAsync(fileContent, extension, cancellationToken);

        var document = new UserDocument
        {
            UserId = targetUserId,
            Type = parsedType,
            FileUrl = fileUrl,
            OriginalFileName = originalFileName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var replacedUrl = await _userDocumentRepository.UpsertAsync(document, cancellationToken);

        // Only once the row points at the new file — deleting first would leave the user with
        // nothing on disk if the write then failed.
        if (replacedUrl is not null)
        {
            await _fileStorage.DeleteAsync(replacedUrl, cancellationToken);
        }

        return Result.Success(await BuildVerificationAsync(user, cancellationToken), "Document uploaded successfully.");
    }

    public async Task<Result<UserVerificationResponse>> GetVerificationAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId != targetUserId && !_currentUser.IsInRole(nameof(UserRole.Admin)))
        {
            throw new UnauthorizedAccessException("You can only view your own verification status.");
        }

        var user = await GetUserOrThrowAsync(targetUserId, cancellationToken);
        return Result.Success(await BuildVerificationAsync(user, cancellationToken));
    }

    private async Task<UserVerificationResponse> BuildVerificationAsync(User user, CancellationToken cancellationToken)
    {
        var documents = await _userDocumentRepository.GetForUserAsync(user.Id, cancellationToken);
        var required = VerificationPolicy.RequiredDocuments(user.Role);
        var submitted = documents.Select(d => d.Type).ToHashSet();
        var missing = required.Where(type => !submitted.Contains(type)).ToList();

        return new UserVerificationResponse(
            user.Id,
            user.Role.ToString(),
            user.AccountStatus.ToString(),
            documents.Select(d => new UserDocumentResponse(d.Id, d.Type.ToString(), d.FileUrl, d.OriginalFileName, d.UpdatedAtUtc)).ToList(),
            required.Select(t => t.ToString()).ToList(),
            missing.Select(t => t.ToString()).ToList(),
            missing.Count == 0 && user.AccountStatus == AccountStatus.Pending);
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
        user.State = request.State;
        user.Pincode = request.Pincode;
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

        if (!ImageFileTypes.IsImage(fileExtension))
        {
            return Result.Failure<AvatarUploadResponse>($"Avatar must be {ImageFileTypes.ImageDescription}.");
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
