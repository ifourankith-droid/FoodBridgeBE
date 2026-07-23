namespace FoodBridge.Application.Auth.Dtos;

/// <summary>
/// Matches the documented `{ isNewUser, token?, user? }` contract. For an existing
/// user, <see cref="Token"/> is a full auth JWT. For a new user, it is a short-lived
/// registration session token (see <c>IPasswordlessSessionService</c>) that
/// <c>POST /api/auth/register</c> requires to prove the mobile was just OTP-verified.
/// </summary>
public sealed record VerifyOtpResponse(
    bool IsNewUser,
    string? Token,
    UserResponse? User);
