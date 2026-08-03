using FoodBridge.Application.Abstractions;
using QuestPDF.Fluent;

namespace FoodBridge.Infrastructure.Pdf;

/// <summary>
/// Adapter: renders <see cref="CertificateDocument"/> to PDF bytes. The layout lives there so this
/// stays the only thing that knows about the output format.
/// </summary>
public sealed class QuestPdfCertificateGenerator : IPdfGenerator
{
    public byte[] GenerateCertificatePdf(CertificatePdfModel model) =>
        CertificateDocument.Create(model).GeneratePdf();
}
