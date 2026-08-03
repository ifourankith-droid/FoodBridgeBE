namespace FoodBridge.Application.Common;

/// <summary>
/// Limits on the OTP login flow. Configurable so a demo can lift them without a code change, and so
/// they can be tuned per environment rather than by editing constants.
/// <para>
/// Its own section rather than part of <see cref="OtpSettings"/> on purpose: that one is only bound
/// when the fixed-code gate is open (see Program.cs), and abuse limits must never depend on whether a
/// demo code is active.
/// </para>
/// <para>
/// Defaults reproduce the original hard-coded behaviour, so an environment that configures nothing
/// keeps the protection. Setting a limit to <c>0</c> (or less) disables that check entirely.
/// </para>
/// </summary>
public sealed class OtpRateLimitSettings
{
    public const string SectionName = "OtpRateLimit";

    /// <summary>
    /// How many <c>send-otp</c> calls one mobile number may make within <see cref="WindowMinutes"/>
    /// before being refused with 429. Counts <b>every</b> OTP issued in the window, including ones
    /// used successfully — a clean login still consumes one.
    /// <para><c>0</c> or less disables the limit: unlimited sends, and the counting query is skipped.</para>
    /// </summary>
    public int MaxSendsPerWindow { get; set; } = 3;

    /// <summary>
    /// The sliding window for <see cref="MaxSendsPerWindow"/>. Sliding, not fixed: capacity returns one
    /// send at a time as individual sends age out, rather than all at once.
    /// </summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// How many wrong codes may be submitted against a single OTP before it is locked and a new one
    /// must be requested. Unlike the send limit this returns 422, not 429.
    /// <para><c>0</c> or less disables the limit: a code can be guessed without bound until it expires.</para>
    /// </summary>
    public int MaxVerifyAttempts { get; set; } = 5;

    /// <summary>True when either limit is switched off — used for the startup security warning.</summary>
    public bool IsAnyLimitDisabled => MaxSendsPerWindow <= 0 || MaxVerifyAttempts <= 0;
}
