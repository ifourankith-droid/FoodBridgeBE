using System.Net.Http.Headers;
using System.Text;
using FoodBridge.Application.Abstractions;
using FoodBridge.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoodBridge.Infrastructure.Auth;

/// <summary>
/// Sends the OTP as a real WhatsApp message via Twilio's WhatsApp Sandbox REST API.
/// Only registered when <see cref="TwilioSettings.Enabled"/> is true (see Program.cs) —
/// see docs/TWILIO_WHATSAPP_SETUP.md for how to get credentials and turn it on.
/// </summary>
public sealed class TwilioWhatsAppSmsProvider : ISmsProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwilioWhatsAppSmsProvider> _logger;
    private readonly TwilioSettings _settings;

    public TwilioWhatsAppSmsProvider(HttpClient httpClient, ILogger<TwilioWhatsAppSmsProvider> logger, IOptions<TwilioSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default)
    {
        var to = mobile.StartsWith("+", StringComparison.Ordinal) ? mobile : $"{_settings.DefaultCountryCode}{mobile}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{_settings.AccountSid}/Messages.json");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.AccountSid}:{_settings.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = _settings.WhatsAppFromNumber,
            ["To"] = $"whatsapp:{to}",
            ["Body"] = $"Your FoodBridge OTP is {code}. It expires in 5 minutes.",
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("[TwilioWhatsApp] OTP sent to {Mobile}", mobile);
    }
}
