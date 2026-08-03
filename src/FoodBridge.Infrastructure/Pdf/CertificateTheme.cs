namespace FoodBridge.Infrastructure.Pdf;

/// <summary>
/// Fixed presentation constants for the donation certificate.
/// <para>
/// Deliberately hard-coded rather than read from the app's theme: a donor can change the accent colour
/// in the UI, and a certificate that is meant to be printed, filed and attached to a CSR report must
/// look identical whenever it is regenerated. The values mirror the frontend's default brand
/// (<c>#0f766e</c> primary, <c>#146c43</c> deep success) so the document still looks like the product.
/// </para>
/// </summary>
internal static class CertificateTheme
{
    // ---- Palette ----
    internal const string Primary = "#0F766E";
    internal const string PrimaryDeep = "#0B534E";
    internal const string Ink = "#16221F";
    internal const string Muted = "#6B7A78";
    internal const string Gold = "#B08833";
    internal const string GoldSoft = "#EFE3C6";
    internal const string Paper = "#FFFEFB";
    internal const string Band = "#F4F8F7";
    internal const string Line = "#DCE5E3";

    /// <summary>
    /// Serif display face for the ceremonial lines. Georgia ships with Windows, which is what the API
    /// runs on (App Service, <c>kind: app</c>); QuestPDF substitutes its bundled Lato if it is ever
    /// absent, which degrades the styling but never fails the render.
    /// </summary>
    internal const string Display = "Georgia";

    /// <summary>Body/label face — QuestPDF bundles Lato, so this resolves on any host.</summary>
    internal const string Body = "Lato";

    /// <summary>
    /// IST as a fixed offset. India has no DST, so this is exact year-round, and a constant avoids
    /// depending on a time-zone database ID that differs between Windows ("India Standard Time") and
    /// Linux ("Asia/Kolkata"). Mirrors the frontend's <c>APP_TIME_ZONE_OFFSET</c>.
    /// </summary>
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromMinutes(330);

    internal static DateTime ToIndiaTime(DateTime utc) => utc + IndiaOffset;
}
