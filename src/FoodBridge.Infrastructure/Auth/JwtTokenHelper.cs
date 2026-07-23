using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using FoodBridge.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoodBridge.Infrastructure.Auth;

public sealed class JwtTokenHelper : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IClock _clock;

    public JwtTokenHelper(IOptions<JwtSettings> settings, IClock clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("name", user.Name),
            new Claim("mobile", user.Mobile),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = _clock.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(_settings.ExpiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
