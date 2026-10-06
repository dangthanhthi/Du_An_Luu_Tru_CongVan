using Das.PdfProtocol;
namespace DocumentService;
public sealed class PdfMaintenance(CurrentPdfService pdf):IPdfMaintenance
{
    public async Task RunAsync(CancellationToken ct){await pdf.ExpirePreparingAsync(ct);await pdf.DispatchAsync(ct);await pdf.RefreshReadinessAsync(ct);}
}
