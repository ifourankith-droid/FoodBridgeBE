namespace FoodBridge.Application.Common;

public sealed class OtpSettings
{
    public const string SectionName = "Otp";

    /// <summary>
    /// When set, every OTP uses this fixed code instead of a random one — lets you log in without
    /// checking the console/log for the code. Honoured automatically in Development; outside it,
    /// <see cref="AllowFixedCodeOutsideDevelopment"/> must also be true (see Program.cs).
    /// </summary>
    public string? FixedDevelopmentCode { get; set; }

    /// <summary>
    /// Opt-in to honouring <see cref="FixedDevelopmentCode"/> outside Development — for a live demo
    /// where reading codes out of a log stream isn't practical.
    /// <para>
    /// <b>This is a real security downgrade, deliberately gated behind its own flag.</b> A fixed code
    /// means anyone who knows a registered mobile number can sign in as that account: strictly weaker
    /// than logging the code, which at least requires access to the logs. Off by default, never set in
    /// any checked-in config, and Program.cs logs a warning on every startup while it's enabled so it
    /// can't be left on unnoticed. Turn it off as soon as the demo is over.
    /// </para>
    /// </summary>
    public bool AllowFixedCodeOutsideDevelopment { get; set; }
}
