using FoodBridge.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FoodBridge.Infrastructure.Auth;

/// <summary>
/// Logs the OTP instead of sending a real SMS. Swap for a real provider
/// (MSG91, Twilio, ...) by implementing <see cref="ISmsProvider"/> — no
/// consumer code changes required.
/// </summary>
public sealed class MockSmsProvider : ISmsProvider
{
    private readonly ILogger<MockSmsProvider> _logger;

    public MockSmsProvider(ILogger<MockSmsProvider> logger)
    {
        _logger = logger;
    }

    public Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MockSms] OTP for {Mobile} is {Code}", mobile, code);
        return Task.CompletedTask;
    }
}
