namespace FoodBridge.Application.Common;

public sealed class TwilioSettings
{
    public const string SectionName = "Twilio";

    /// <summary>
    /// Master switch, false everywhere checked in. ISmsProvider stays MockSmsProvider
    /// (logs the OTP, sends nothing) until this is explicitly set true with real
    /// credentials — see docs/TWILIO_WHATSAPP_SETUP.md.
    /// </summary>
    public bool Enabled { get; set; }

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Twilio's WhatsApp Sandbox number, e.g. "whatsapp:+14155238886".</summary>
    public string WhatsAppFromNumber { get; set; } = "whatsapp:+14155238886";

    /// <summary>Prefixed onto a bare mobile number before dialing, e.g. "+91".</summary>
    public string DefaultCountryCode { get; set; } = "+91";
}
