using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

        if (!response.IsSuccessStatusCode)
        {
            // Twilio explains every rejection in the response body — an error `code`, a `message`
            // naming the exact field at fault, and a `more_info` doc link. This used to call
            // EnsureSuccessStatusCode(), which discarded all of it and left only "Response status
            // code does not indicate success: 400", making a bad From number, a lapsed sandbox
            // opt-in and a wrong credential completely indistinguishable.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = DescribeTwilioError(body);

            // Deliberately logs neither `request` nor the credential: that Authorization header is
            // just Base64 of "AccountSid:AuthToken", so including the request in an error would
            // write a live secret into the logs — and into wherever those logs get pasted.
            _logger.LogError(
                "[TwilioWhatsApp] Twilio rejected the message for {Mobile}. HTTP {Status}. {Detail} From={From}. " +
                "See docs/TWILIO_WHATSAPP_SETUP.md: From must be a Twilio sender in E.164 form " +
                "(sandbox: whatsapp:+14155238886) — never your own number — and the recipient must have " +
                "sent the sandbox join code from that same WhatsApp account.",
                mobile,
                (int)response.StatusCode,
                detail,
                _settings.WhatsAppFromNumber);

            throw new InvalidOperationException(
                $"Twilio rejected the WhatsApp message (HTTP {(int)response.StatusCode}). {detail}");
        }

        _logger.LogInformation("[TwilioWhatsApp] OTP sent to {Mobile}", mobile);
    }

    /// <summary>
    /// Extracts Twilio's `code`/`message`/`more_info` from an error body, falling back to the raw
    /// text when it isn't the expected shape — an unparseable body still beats no detail at all.
    /// </summary>
    private static string DescribeTwilioError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Twilio returned no error details.";
        }

        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.ToString() : null;
            var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            var moreInfo = root.TryGetProperty("more_info", out var i) ? i.GetString() : null;

            if (code is null && message is null)
            {
                return Truncate(body);
            }

            var parts = new List<string>();
            if (code is not null)
            {
                parts.Add($"Twilio error {code}");
            }

            if (message is not null)
            {
                parts.Add(message);
            }

            if (moreInfo is not null)
            {
                parts.Add($"See {moreInfo}");
            }

            return string.Join(" — ", parts);
        }
        catch (JsonException)
        {
            return Truncate(body);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";
}
