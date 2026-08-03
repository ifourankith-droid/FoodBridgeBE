namespace FoodBridge.Application.Auth.Dtos;

/// <summary>
/// <paramref name="SessionToken"/> is the `token` value returned by
/// <c>POST /api/auth/verify-otp</c> when <c>isNewUser</c> was true.
/// <paramref name="RecipientType"/> ("Individual" or "Organization") is required
/// when <paramref name="Role"/> is Recipient, ignored otherwise.
/// </summary>
/// <param name="State">Optional. Reverse-geocoding fills it from the picked pin.</param>
/// <param name="Pincode">
/// Optional, display-only — distance is always computed from the coordinates, never the pincode.
/// </param>
public sealed record RegisterRequest(
    string SessionToken,
    string Role,
    string Name,
    string? City,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? RecipientType,
    int? CapacityMeals,
    string? State = null,
    string? Pincode = null);
