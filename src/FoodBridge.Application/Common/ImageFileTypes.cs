namespace FoodBridge.Application.Common;

/// <summary>
/// The one place that decides whether an uploaded file counts as a picture.
/// Every upload path reads it — listing photos, pickup/delivery proof, avatars and
/// verification documents — so a photo the browser let someone pick can never be
/// accepted by one endpoint and rejected by another. Three hand-maintained copies of
/// this list is exactly how ".jfif" ended up being refused only on some screens.
/// </summary>
public static class ImageFileTypes
{
    /// <summary>
    /// Every raster format a browser can display, including the JPEG aliases Windows
    /// hands out — ".jfif" in particular is what Chrome's "Save image as" writes, so
    /// users hit it constantly. Deliberately excluded: SVG (a scriptable XML document
    /// rather than a picture, so serving user-supplied ones is stored XSS), and
    /// HEIC/TIFF, which upload fine but render as a broken image in most desktop
    /// browsers — a clear "not supported" beats a photo that silently shows as nothing.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".jif", ".jpe", ".pjpeg", ".pjp",
        ".png", ".apng", ".webp", ".avif", ".gif", ".bmp", ".dib",
    };

    /// <summary>Wording for the rejection message, so every endpoint says the same thing.</summary>
    public const string ImageDescription = "a JPG, PNG, WebP, AVIF, GIF or BMP image";

    /// <summary>Same list plus PDF — a scanned ID proof is very often a PDF.</summary>
    public const string DocumentDescription = ImageDescription + ", or a PDF";

    public static bool IsImage(string? fileExtension) =>
        !string.IsNullOrWhiteSpace(fileExtension) && ImageExtensions.Contains(fileExtension.Trim());

    public static bool IsImageOrPdf(string? fileExtension) =>
        IsImage(fileExtension) ||
        ".pdf".Equals(fileExtension?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Content types for the extensions the static-file provider's default map doesn't know
    /// (or maps to something browsers refuse, as it does for image/pjpeg). Applied to the
    /// static-file provider in Program.cs: an extension we accept but can't serve would save the
    /// upload and then 404 the picture.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ExtraContentTypes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jfif"] = "image/jpeg",
            [".jif"] = "image/jpeg",
            [".jpe"] = "image/jpeg",
            [".pjpeg"] = "image/jpeg",
            [".pjp"] = "image/jpeg",
            [".apng"] = "image/apng",
            [".avif"] = "image/avif",
            [".webp"] = "image/webp",
            [".dib"] = "image/bmp",
        };
}
