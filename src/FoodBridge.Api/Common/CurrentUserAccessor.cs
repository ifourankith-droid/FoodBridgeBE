using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FoodBridge.Application.Abstractions;

namespace FoodBridge.Api.Common;

public sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal Principal =>
        _httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException("No authenticated user for this request.");

    public Guid UserId => Guid.Parse(FindClaim(JwtRegisteredClaimNames.Sub));

    public string Role => FindClaim(ClaimTypes.Role);

    public string Mobile => FindClaim("mobile");

    public string TokenId => FindClaim(JwtRegisteredClaimNames.Jti);

    public DateTime TokenExpiresAtUtc =>
        DateTimeOffset.FromUnixTimeSeconds(long.Parse(FindClaim(JwtRegisteredClaimNames.Exp))).UtcDateTime;

    public bool IsInRole(string role) => Principal.IsInRole(role);

    private string FindClaim(string claimType) =>
        Principal.Claims.FirstOrDefault(c => c.Type == claimType)?.Value
        ?? throw new UnauthorizedAccessException($"Missing '{claimType}' claim.");
}
