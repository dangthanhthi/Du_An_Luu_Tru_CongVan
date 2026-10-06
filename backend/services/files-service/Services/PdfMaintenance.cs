using Das.PdfProtocol;
namespace FilesService.Services;
public sealed class PdfMaintenance(PdfClaims claims,PdfUploadMaintenance uploads):IPdfMaintenance
{
    public async Task RunAsync(CancellationToken ct){await claims.ReconcileAsync(ct);await uploads.RunAsync(ct);}
}
