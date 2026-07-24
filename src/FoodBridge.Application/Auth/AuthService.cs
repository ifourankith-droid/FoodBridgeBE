using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Auth.Dtos;
using FoodBridge.Application.Common;
using FoodBridge.Domain.Entities;
using FoodBridge.Domain.Enums;
using FoodBridge.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace FoodBridge.Application.Auth;

public sealed class AuthService : IAuthService
{
    private const int MaxSendsPerWindow = 3;
    private static readonly TimeSpan SendWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(5);
    private const int MaxVerifyAttempts = 5;

    private readonly IUserRepository _userRepository;
    private readonly IOtpCodeRepository _otpCodeRepository;
    private readonly ISmsProvider _smsProvider;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordlessSessionService _passwordlessSessionService;
    private readonly ITokenDenylist _tokenDenylist;
    private readonly IClock _clock;
    private readonly OtpSettings _otpSettings;

    public AuthService(
        IUserRepository userRepository,
        IOtpCodeRepository otpCodeRepository,
        ISmsProvider smsProvider,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordlessSessionService passwordlessSessionService,
        ITokenDenylist tokenDenylist,
        IClock clock,
        IOptions<OtpSettings> otpSettings)
    {
        _userRepository = userRepository;
        _otpCodeRepository = otpCodeRepository;
        _smsProvider = smsProvider;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordlessSessionService = passwordlessSessionService;
        _tokenDenylist = tokenDenylist;
        _clock = clock;
        _otpSettings = otpSettings.Value;
    }

    public async Task<Result> SendOtpAsync(SendOtpRequest request, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var sentCount = await _otpCodeRepository.CountSentSinceAsync(request.Mobile, now - SendWindow, cancellationToken);
        if (sentCount >= MaxSendsPerWindow)
        {
            throw new RateLimitExceededException("Too many OTP requests. Please try again later.");
        }

        var code = string.IsNullOrWhiteSpace(_otpSettings.FixedDevelopmentCode)
            ? OtpGenerator.GenerateCode()
            : _otpSettings.FixedDevelopmentCode;
        var otpCode = new OtpCode
        {
            Id = Guid.NewGuid(),
            Mobile = request.Mobile,
            CodeHash = OtpGenerator.Hash(request.Mobile, code),
            ExpiresAtUtc = now + OtpValidity,
            Attempts = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _otpCodeRepository.CreateAsync(otpCode, cancellationToken);
        await _smsProvider.SendOtpAsync(request.Mobile, code, cancellationToken);

        return Result.Success("OTP sent successfully.");
    }

    public async Task<Result<VerifyOtpResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var otp = await _otpCodeRepository.GetLatestActiveAsync(request.Mobile, cancellationToken);
        if (otp is null)
        {
            return Result.Failure<VerifyOtpResponse>("OTP not found or has expired.");
        }

        if (otp.Attempts >= MaxVerifyAttempts)
        {
            return Result.Failure<VerifyOtpResponse>("Maximum verification attempts exceeded. Please request a new OTP.");
        }

        if (!OtpGenerator.Verify(request.Mobile, request.Code, otp.CodeHash))
        {
            await _otpCodeRepository.IncrementAttemptsAsync(otp.Id, cancellationToken);
            return Result.Failure<VerifyOtpResponse>("Invalid OTP.");
        }

        await _otpCodeRepository.ConsumeAsync(otp.Id, cancellationToken);

        var user = await _userRepository.GetByMobileAsync(request.Mobile, cancellationToken);
        if (user is not null)
        {
            var token = _jwtTokenGenerator.GenerateToken(user);
            return Result.Success(new VerifyOtpResponse(false, token, user.ToResponse()));
        }

        var sessionToken = _passwordlessSessionService.IssueSessionToken(request.Mobile);
        return Result.Success(new VerifyOtpResponse(true, sessionToken, null));
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var mobile = _passwordlessSessionService.ValidateSessionToken(request.SessionToken);
        if (mobile is null)
        {
            return Result.Failure<AuthResponse>("Session expired or invalid. Please verify your mobile again.");
        }

        var existing = await _userRepository.GetByMobileAsync(mobile, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An account with this mobile number already exists.");
        }

        var role = Enum.Parse<UserRole>(request.Role, ignoreCase: true);
        var accountStatus = role == UserRole.Recipient ? AccountStatus.Pending : AccountStatus.Verified;
        var now = _clock.UtcNow;

        var user = new User
        {
            Mobile = mobile,
            Name = request.Name,
            Role = role,
            City = request.City,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RecipientType = role == UserRole.Recipient
                ? Enum.Parse<RecipientType>(request.RecipientType!, ignoreCase: true)
                : null,
            CapacityMeals = role == UserRole.Recipient ? request.CapacityMeals : null,
            IsAvailable = true,
            AccountStatus = accountStatus,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        user.Id = await _userRepository.CreateAsync(user, cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        return Result.Success(new AuthResponse(token, user.ToResponse()));
    }

    public Task<Result> LogoutAsync(string jti, DateTime tokenExpiresAtUtc, CancellationToken cancellationToken = default)
    {
        _tokenDenylist.Add(jti, tokenExpiresAtUtc);
        return Task.FromResult(Result.Success("Logged out successfully."));
    }

    public async Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User", userId);
        }

        return Result.Success(user.ToResponse());
    }
}
