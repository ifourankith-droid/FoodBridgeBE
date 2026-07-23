using FoodBridge.Application.Auth.Dtos;
using FoodBridge.Application.Common;

namespace FoodBridge.Application.Auth;

public interface IAuthService
{
    Task<Result> SendOtpAsync(SendOtpRequest request, CancellationToken cancellationToken = default);

    Task<Result<VerifyOtpResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(string jti, DateTime tokenExpiresAtUtc, CancellationToken cancellationToken = default);

    Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
}
