namespace DocumentService;

// Resolve from provisioned, current server authority. Never bind this from browser DTO/roles.
public interface IPdfAuthority
{
    Task<V2EditorActor> EditorAsync(Guid documentId,Guid userId,CancellationToken ct);
    Task<bool> CanReadAsync(Guid documentId,Guid userId,CancellationToken ct);
}
public sealed class UnavailablePdfAuthority:IPdfAuthority
{
    public Task<V2EditorActor> EditorAsync(Guid d,Guid u,CancellationToken ct)=>throw Unavailable();
    public Task<bool> CanReadAsync(Guid d,Guid u,CancellationToken ct)=>throw Unavailable();
    private static DocumentRegistrationRuleException Unavailable()=>new(503,"PDF_AUTHORITY_UNAVAILABLE","Trusted document authority is not configured.");
}
