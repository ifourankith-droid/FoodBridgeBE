using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using FoodBridge.Application.Auth;
using FoodBridge.Application.Auth.Dtos;
using FoodBridge.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// OTP-based login/registration and JWT issuance.
/// </summary>
[Route("api/auth")]
public sealed class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly IValidator<SendOtpRequest> _sendOtpValidator;
    private readonly IValidator<VerifyOtpRequest> _verifyOtpValidator;
    private readonly IValidator<RegisterRequest> _registerValidator;

    public AuthController(
        IAuthService authService,
        IValidator<SendOtpRequest> sendOtpValidator,
        IValidator<VerifyOtpRequest> verifyOtpValidator,
        IValidator<RegisterRequest> registerValidator)
    {
        _authService = authService;
        _sendOtpValidator = sendOtpValidator;
        _verifyOtpValidator = verifyOtpValidator;
        _registerValidator = registerValidator;
    }

    /// <summary>
    /// Sends a 6-digit OTP to the given mobile number (max 3 per 15 minutes).
    /// </summary>
    [HttpPost("send-otp")]
    public async Task<ActionResult<ApiResponse<object?>>> SendOtp([FromBody] SendOtpRequest request, CancellationToken cancellationToken)
    {
        await _sendOtpValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _authService.SendOtpAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Verifies an OTP (max 5 attempts). Returns a JWT for an existing user, or a
    /// short-lived registration session token when the mobile has no account yet.
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<ActionResult<ApiResponse<VerifyOtpResponse>>> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        await _verifyOtpValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _authService.VerifyOtpAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Completes registration for a mobile that was just OTP-verified.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Revokes the current JWT by adding its id to the in-memory denylist.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object?>>> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti)!;
        var expUnixSeconds = long.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Exp)!);
        var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds).UtcDateTime;

        var result = await _authService.LogoutAsync(jti, expiresAtUtc, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Returns the current authenticated user's profile.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Me(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var result = await _authService.GetMeAsync(userId, cancellationToken);
        return HandleResult(result);
    }
}
