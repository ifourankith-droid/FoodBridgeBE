using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoodBridge.Infrastructure.Auth;

/// <summary>
/// Short-lived (10 min) signed token proving a mobile number was just OTP-verified.
/// Not a full auth JWT — carries only a mobile claim, used solely to authorize
/// <c>POST /api/auth/register</c> for a not-yet-existing user.
/// </summary>
public sealed class PasswordlessSessionHelper : IPasswordlessSessionService
{
    private const string MobileClaimType = "mobile";
    private const string PurposeClaimType = "purpose";
    private const string RegistrationPurpose = "registration";
    private static readonly TimeSpan SessionValidity = TimeSpan.FromMinutes(10);

    private readonly JwtSettings _settings;
    private readonly IClock _clock;

    public PasswordlessSessionHelper(IOptions<JwtSettings> settings, IClock clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public string IssueSessionToken(string mobile)
    {
        var claims = new[]
        {
            new Claim(MobileClaimType, mobile),
            new Claim(PurposeClaimType, RegistrationPurpose),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = _clock.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now + SessionValidity,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? ValidateSessionToken(string sessionToken)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(sessionToken, validationParameters, out _);
            var purpose = principal.Claims.FirstOrDefault(c => c.Type == PurposeClaimType)?.Value;
            if (purpose != RegistrationPurpose)
            {
                return null;
            }

            return principal.Claims.FirstOrDefault(c => c.Type == MobileClaimType)?.Value;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
