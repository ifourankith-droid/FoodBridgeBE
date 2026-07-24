namespace FoodBridge.Application.Common;

public sealed class OtpSettings
{
    public const string SectionName = "Otp";

    /// <summary>
    /// When set, every OTP uses this fixed code instead of a random one — lets you
    /// log in without checking the console/log for the code. Only ever bound from
    /// appsettings.Development.json, and Program.cs only registers this section
    /// when IsDevelopment(), so it can never take effect outside local dev even if
    /// the key leaks into a non-dev config file.
    /// </summary>
    public string? FixedDevelopmentCode { get; set; }
}
