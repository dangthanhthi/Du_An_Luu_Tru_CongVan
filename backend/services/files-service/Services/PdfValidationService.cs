using UglyToad.PdfPig;
namespace FilesService.Services;

public sealed class PdfValidationService
{
    public void Validate(Stream stream, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Span<byte> magic = stackalloc byte[5]; stream.Position = 0;
        if (stream.Read(magic) != 5 || !magic.SequenceEqual("%PDF-"u8)) throw Invalid();
        stream.Position = 0;
        try {
            using var pdf = PdfDocument.Open(stream, new ParsingOptions { UseLenientParsing = false });
            if (pdf.IsEncrypted || pdf.NumberOfPages is < 1 or > 1000) throw Invalid();
            for (var page = 1; page <= pdf.NumberOfPages; page++) { ct.ThrowIfCancellationRequested(); _ = pdf.GetPage(page); }
        } catch (OperationCanceledException) { throw; }
        catch (FileRuleException) { throw; }
        catch (Exception error) when (error is not (OutOfMemoryException or IOException)) { throw Invalid(); }
        finally { stream.Position = 0; }
    }
    private static FileRuleException Invalid() => new(422, "INVALID_PDF", "The PDF is malformed, encrypted or outside the supported page limit.");
}
public enum PdfScanResult { Clean, Malicious, Unavailable }
public interface IPdfThreatScanner { Task<PdfScanResult> ScanAsync(Stream stream, CancellationToken ct); }
public sealed class UnavailablePdfThreatScanner : IPdfThreatScanner
{
    public Task<PdfScanResult> ScanAsync(Stream stream, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(PdfScanResult.Unavailable); }
}
