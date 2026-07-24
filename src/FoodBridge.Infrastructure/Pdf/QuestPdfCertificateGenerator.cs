using FoodBridge.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FoodBridge.Infrastructure.Pdf;

public sealed class QuestPdfCertificateGenerator : IPdfGenerator
{
    public byte[] GenerateCertificatePdf(CertificatePdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(14));

                page.Content().Column(column =>
                {
                    column.Spacing(16);

                    column.Item().AlignCenter().Text("FoodBridge").FontSize(28).Bold();
                    column.Item().AlignCenter().Text("Certificate of Appreciation").FontSize(18).SemiBold();

                    column.Item().PaddingTop(20).AlignCenter().Text("This certificate is proudly presented to");
                    column.Item().AlignCenter().Text(model.DonorName).FontSize(22).Bold();

                    column.Item().AlignCenter().Text(
                        $"in recognition of donating {model.MealsCount} meal(s) through \"{model.ListingTitle}\", " +
                        "helping fight food waste and hunger in the community.");

                    column.Item().PaddingTop(30).Text($"Certificate Number: {model.CertificateNumber}");
                    column.Item().Text($"Issued: {model.IssuedAtUtc:dd MMMM yyyy}");
                });
            });
        });

        return document.GeneratePdf();
    }
}
