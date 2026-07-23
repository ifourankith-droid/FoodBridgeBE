namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Issues and validates the short-lived token that proves a mobile number was
/// just OTP-verified, without yet having a user account to issue a full JWT for.
/// </summary>
public interface IPasswordlessSessionService
{
    string IssueSessionToken(string mobile);

    string? ValidateSessionToken(string sessionToken);
}
