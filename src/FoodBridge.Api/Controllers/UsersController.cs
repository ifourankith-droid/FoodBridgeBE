using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Users;
using FoodBridge.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// User profile, availability, and avatar management.
/// </summary>
[Authorize]
[Route("api/users")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class UsersController : BaseController
{
    private readonly IUserService _userService;
    private readonly IValidator<UpdateUserRequest> _updateUserValidator;

    public UsersController(IUserService userService, IValidator<UpdateUserRequest> updateUserValidator)
    {
        _userService = userService;
        _updateUserValidator = updateUserValidator;
    }

    /// <summary>
    /// Returns a user's profile. Callable by the user themselves or an Admin.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetProfileAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates a user's profile. Self only. Updating latitude/longitude also
    /// updates the underlying geography column.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _updateUserValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _userService.UpdateProfileAsync(id, request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Toggles availability. Self only; volunteers and recipients only.
    /// </summary>
    [HttpPatch("{id:guid}/availability")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> UpdateAvailability(Guid id, [FromBody] UpdateAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateAvailabilityAsync(id, request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Uploads an avatar (JPG/JFIF/PNG/WebP/AVIF/GIF/BMP, max 2MB). Self only.
    /// </summary>
    /// <summary>
    /// Verification status and submitted documents. Self or admin — the volunteer tracks their own
    /// progress here, and the admin reads the same payload to review it before verifying.
    /// </summary>
    [HttpGet("{id:guid}/verification")]
    [ProducesResponseType(typeof(ApiResponse<UserVerificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserVerificationResponse>>> GetVerification(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetVerificationAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Uploads or replaces one verification document (<c>type</c>: <c>IdProof</c> or <c>Selfie</c>).
    /// Self only — evidence is only meaningful if it came from the person it describes. Re-uploading
    /// the same type replaces it, and the superseded file is deleted.
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UserVerificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<UserVerificationResponse>>> UploadDocument(
        Guid id,
        IFormFile? file,
        [FromForm] string? type,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UserVerificationResponse>.Fail("A file is required.", traceId: TraceId));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            return BadRequest(ApiResponse<UserVerificationResponse>.Fail("A document type is required (IdProof or Selfie).", traceId: TraceId));
        }

        var extension = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        var result = await _userService.UploadDocumentAsync(id, type, stream, extension, file.Length, file.FileName, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AvatarUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AvatarUploadResponse>>> UploadAvatar(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<AvatarUploadResponse>.Fail("A file is required.", traceId: TraceId));
        }

        var extension = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        var result = await _userService.UploadAvatarAsync(id, stream, extension, file.Length, cancellationToken);
        return HandleResult(result);
    }
}
