using FoodBridge.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static FoodBridge.Infrastructure.Pdf.CertificateTheme;

namespace FoodBridge.Infrastructure.Pdf;

/// <summary>
/// The donation certificate's layout: a landscape A4 document a donor can print, frame, or attach to a
/// CSR report.
/// <para>
/// Separate from <see cref="QuestPdfCertificateGenerator"/>, which only turns a document into bytes.
/// Keeping the composition here lets the layout be rasterised for visual review without going through
/// the PDF byte stream, and leaves the adapter a two-liner.
/// </para>
/// <para>
/// Composed of bands (frame → crest → award → recipient → citation → impact → footer) so each piece can
/// be read and adjusted on its own. Every effect is typography, rule and fill — QuestPDF's Community
/// licence covers all of it, with no image assets to ship and no fonts to embed.
/// </para>
/// </summary>
public static class CertificateDocument
{
    public static IDocument Create(CertificatePdfModel model) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                // Landscape: the conventional orientation for an award, and it gives the donor's name
                // the horizontal room to stay on one line at display size.
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0);
                page.PageColor(Paper);
                page.DefaultTextStyle(x => x.FontFamily(Body).FontSize(11).FontColor(Ink));

                page.Content().Element(e => Frame(e, model));
            });
        });

    /// <summary>
    /// The double border. An outer rule in the brand colour with an inset gold hairline — the cheapest
    /// way to make a page read as a certificate rather than a letter.
    /// </summary>
    private static void Frame(IContainer container, CertificatePdfModel model)
    {
        container
            // Extend() first, so the frame occupies the full page. Without it the frame is only as
            // tall as its content, the inner column's Extend() has no bounded remainder to consume,
            // and the impact band and footer are pushed onto a second page.
            .Extend()
            .Padding(16)
            .Border(2.5f)
            .BorderColor(PrimaryDeep)
            .Padding(5)
            .Border(0.75f)
            .BorderColor(Gold)
            .Padding(26)
            .Element(e => Bands(e, model));
    }

    /// <summary>
    /// Two layers rather than one column: the ceremonial text flows from the top, and the impact band
    /// plus signature block are pinned to the bottom of the page, so every certificate has an identical
    /// layout however many lines the citation wraps to.
    /// <para>
    /// A single column with an <c>Extend()</c> spacer between the two groups does not work — in a
    /// Column, <c>Extend()</c> asks for unbounded height rather than absorbing the remainder, which
    /// pushed the bottom group onto a second page.
    /// </para>
    /// </summary>
    private static void Bands(IContainer container, CertificatePdfModel model)
    {
        container.Layers(layers =>
        {
            layers.PrimaryLayer().Column(column =>
            {
                column.Item().Element(Crest);
                column.Item().PaddingTop(14).Element(Award);
                column.Item().PaddingTop(18).Element(e => Recipient(e, model));
                column.Item().PaddingTop(14).Element(e => Citation(e, model));
            });

            layers.Layer().AlignBottom().Column(column =>
            {
                column.Item().Element(e => Impact(e, model));
                column.Item().PaddingTop(20).Element(Footer);
            });
        });
    }

    /// <summary>Wordmark and tagline, centred, above a ruled ornament.</summary>
    private static void Crest(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("FOODBRIDGE")
                .FontFamily(Display).FontSize(19).Bold().FontColor(PrimaryDeep).LetterSpacing(0.32f);

            column.Item().PaddingTop(3).AlignCenter().Text("RESCUE FOOD · RESTORE HOPE")
                .FontSize(7).FontColor(Muted).LetterSpacing(0.34f);

            column.Item().PaddingTop(10).AlignCenter().Element(Divider);
        });
    }

    /// <summary>
    /// A short centred rule broken by a bead — a classic certificate ornament.
    /// <para>
    /// The bead is a filled box, not a "◆" character: Lato has no U+25C6, so the glyph rendered as
    /// tofu. Geometry always draws, whatever the font.
    /// </para>
    /// </summary>
    private static void Divider(IContainer container)
    {
        container.Row(row =>
        {
            row.ConstantItem(96).AlignMiddle().Height(0.75f).Background(Gold);
            row.ConstantItem(18).AlignCenter().AlignMiddle().Width(4.5f).Height(4.5f).Background(Gold);
            row.ConstantItem(96).AlignMiddle().Height(0.75f).Background(Gold);
        });
    }

    private static void Award(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("CERTIFICATE OF APPRECIATION")
                .FontFamily(Display).FontSize(27).FontColor(Ink).LetterSpacing(0.09f);

            column.Item().PaddingTop(6).AlignCenter().Text("FOR THE RESCUE AND DONATION OF SURPLUS FOOD")
                .FontSize(7.5f).FontColor(Muted).LetterSpacing(0.22f);
        });
    }

    /// <summary>"Presented to" + the donor's name over a ruled line, the way a certificate inscribes it.</summary>
    private static void Recipient(IContainer container, CertificatePdfModel model)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("This certificate is proudly presented to")
                .FontSize(10.5f).FontColor(Muted).Italic();

            column.Item().PaddingTop(6).AlignCenter().Text(model.DonorName)
                .FontFamily(Display).FontSize(30).Bold().FontColor(PrimaryDeep);

            // The rule under the name is what makes it read as an inscription rather than a heading.
            column.Item().PaddingTop(7).AlignCenter().Width(360).Height(0.9f).Background(Line);

            if (!string.IsNullOrWhiteSpace(model.DonorCity))
            {
                column.Item().PaddingTop(6).AlignCenter().Text(model.DonorCity!.Trim().ToUpperInvariant())
                    .FontSize(7.5f).FontColor(Muted).LetterSpacing(0.2f);
            }
        });
    }

    /// <summary>The citation sentence. Held to a narrow measure so it stays readable.</summary>
    private static void Citation(IContainer container, CertificatePdfModel model)
    {
        var meals = model.MealsCount == 1 ? "1 meal" : $"{model.MealsCount:N0} meals";
        var food = string.IsNullOrWhiteSpace(model.FoodType) ? null : model.FoodType!.Trim();

        container.AlignCenter().MaxWidth(500).Text(text =>
        {
            text.AlignCenter();
            text.DefaultTextStyle(x => x.FontSize(11.5f).FontColor(Ink).LineHeight(1.55f));

            text.Span("in recognition of donating ");
            text.Span(meals).SemiBold().FontColor(PrimaryDeep);
            text.Span(food is null ? " through " : $" of {food} through ");
            text.Span($"“{model.ListingTitle}”").Italic();
            text.Span(" — food that reached people who needed it instead of going to waste.");
        });
    }

    /// <summary>
    /// The figures, given prominence. Meals only: there is no CO₂ or equivalent measure behind the
    /// platform, and an invented one on a document a donor may hand to an auditor would be worse than
    /// a missing one.
    /// </summary>
    private static void Impact(IContainer container, CertificatePdfModel model)
    {
        var issued = ToIndiaTime(model.IssuedAtUtc);

        container.Background(Band).Border(0.75f).BorderColor(Line).Padding(14).Row(row =>
        {
            row.RelativeItem().Element(e => Stat(e,
                $"{model.MealsCount:N0}",
                model.MealsCount == 1 ? "MEAL RESCUED" : "MEALS RESCUED"));

            row.ConstantItem(1).Background(Line);
            row.RelativeItem().Element(e => Stat(e, issued.ToString("dd MMM yyyy"), "DATE OF ISSUE"));

            row.ConstantItem(1).Background(Line);
            row.RelativeItem().Element(e => Stat(e, model.CertificateNumber, "CERTIFICATE NO."));
        });
    }

    private static void Stat(IContainer container, string value, string label)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text(value)
                .FontFamily(Display).FontSize(15).Bold().FontColor(PrimaryDeep);
            column.Item().PaddingTop(3).AlignCenter().Text(label)
                .FontSize(6.5f).FontColor(Muted).LetterSpacing(0.24f);
        });
    }

    /// <summary>Seal on the left, signature block on the right — the two marks of authenticity.</summary>
    private static void Footer(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Element(Seal);

            row.RelativeItem().AlignRight().AlignBottom().Column(column =>
            {
                column.Item().AlignRight().Width(190).Height(0.9f).Background(Ink);
                column.Item().PaddingTop(5).AlignRight().Text("FoodBridge")
                    .FontFamily(Display).FontSize(10.5f).SemiBold().FontColor(Ink);
                column.Item().AlignRight().Text("Authorised Signatory")
                    .FontSize(7).FontColor(Muted).LetterSpacing(0.2f);
            });
        });
    }

    /// <summary>
    /// A medallion built from nested borders rather than an image — QuestPDF has no circle primitive,
    /// and a square seal on a gold fill reads as intentional where a clipped circle would not.
    /// </summary>
    private static void Seal(IContainer container)
    {
        container.Width(78).Height(78)
            .Background(GoldSoft)
            .Border(1.5f).BorderColor(Gold)
            .Padding(4)
            .Border(0.5f).BorderColor(Gold)
            .AlignMiddle()
            .Column(column =>
            {
                column.Item().AlignCenter().Text("VERIFIED")
                    .FontSize(6.5f).Bold().FontColor(PrimaryDeep).LetterSpacing(0.18f);
                // A rule rather than a glyph, for the same reason as Divider's bead.
                column.Item().PaddingVertical(4).AlignCenter().Width(24).Height(0.8f).Background(Gold);
                column.Item().AlignCenter().Text("FOOD")
                    .FontSize(6.5f).Bold().FontColor(PrimaryDeep).LetterSpacing(0.14f);
                column.Item().AlignCenter().Text("DONATION")
                    .FontSize(6.5f).Bold().FontColor(PrimaryDeep).LetterSpacing(0.14f);
            });
    }
}
