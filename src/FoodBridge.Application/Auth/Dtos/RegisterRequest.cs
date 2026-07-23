namespace FoodBridge.Application.Auth.Dtos;

/// <summary>
/// <paramref name="SessionToken"/> is the `token` value returned by
/// <c>POST /api/auth/verify-otp</c> when <c>isNewUser</c> was true.
/// <paramref name="RecipientType"/> ("Individual" or "Organization") is required
/// when <paramref name="Role"/> is Recipient, ignored otherwise.
/// </summary>
public sealed record RegisterRequest(
    string SessionToken,
    string Role,
    string Name,
    string? City,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? RecipientType,
    int? CapacityMeals);
