namespace Das.PdfProtocol;

public sealed record PdfPrepare(Guid OperationId, Guid DocumentId, Guid FileId, Guid UploaderUserId, long ExpectedVersion);
public sealed record PdfReceipt(Guid OperationId, Guid DocumentId, Guid FileId, Guid UploaderUserId,
    string OriginalName, long SizeBytes, string Sha256, string State);
public sealed record PdfOperation(Guid OperationId, Guid DocumentId, Guid FileId, string State, bool CurrentReady,
    Guid ActorUserId, long ExpectedVersion);
public interface IPdfFilesClient
{
    Task<PdfReceipt> PrepareAsync(PdfPrepare request, CancellationToken ct);
    Task<PdfReceipt> ActivateAsync(Guid operationId, CancellationToken ct);
    Task RetireAsync(Guid operationId, CancellationToken ct);
    Task<PdfReceipt> InspectAsync(Guid operationId, CancellationToken ct);
}
public interface IPdfDocumentClient
{
    Task<PdfOperation> OperationAsync(Guid operationId, CancellationToken ct);
    Task<bool> CanReadAsync(Guid documentId, Guid fileId, Guid operationId, Guid userId, CancellationToken ct);
}
public sealed class PdfProtocolException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
