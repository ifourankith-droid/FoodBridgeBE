using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Application.Users;
using FoodBridge.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// User profile, availability, and avatar management.
/// </summary>
[Authorize]
[Route("api/users")]
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
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> UpdateAvailability(Guid id, [FromBody] UpdateAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateAvailabilityAsync(id, request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Uploads an avatar (JPG/PNG, max 2MB). Self only.
    /// </summary>
    [HttpPost("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
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
