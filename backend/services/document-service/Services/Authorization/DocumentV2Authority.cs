namespace DocumentService;

// Server-only contracts. Implementations must verify current identity, membership and
// policy; neither HTTP drafts, JWT role strings nor report scopes can supply these facts.
public sealed record V2RegistrationAuthority(V2EditorActor Actor, RegistrationIdentity Identity,
    V2ReferenceSet? References = null, V2RelationScope? Relations = null);
public sealed record V2MutationAuthority(V2EditorActor Actor, V2EditTarget? Target = null,
    V2ReferenceSet? References = null, V2RelationScope? Relations = null);
public sealed record V2ReadAuthority(V2EditorActor Actor, IReadOnlySet<Guid> ReadableDocumentIds);
public sealed record V2RegistrationTarget(Guid OriginatorUserId, string OriginatorName,
    Guid DepartmentId, string DepartmentCode, string DepartmentName, bool IsPrimary);
public sealed record V2FormAuthority(Guid UserId, bool IsActive, bool CanRegister,
    IReadOnlyList<V2RegistrationTarget> Targets);

public interface IDocumentV2Authority
{
    Task<V2RegistrationAuthority> RegisterAsync(Guid userId, V2RegistrationDraft draft, CancellationToken ct);
    Task<V2MutationAuthority> MutateAsync(Guid userId, Guid documentId, V2EditDraft? draft, CancellationToken ct);
    Task<V2ReadAuthority> ReadAsync(Guid userId, CancellationToken ct);
    Task<V2FormAuthority> FormAsync(Guid userId, string kind, CancellationToken ct);
}

public sealed class UnavailableDocumentV2Authority : IDocumentV2Authority
{
    public Task<V2RegistrationAuthority> RegisterAsync(Guid u,V2RegistrationDraft d,CancellationToken ct)=>throw Offline();
    public Task<V2MutationAuthority> MutateAsync(Guid u,Guid d,V2EditDraft? e,CancellationToken ct)=>throw Offline();
    public Task<V2ReadAuthority> ReadAsync(Guid u,CancellationToken ct)=>throw Offline();
    public Task<V2FormAuthority> FormAsync(Guid u,string k,CancellationToken ct)=>throw Offline();
    private static DocumentRegistrationRuleException Offline()=>new(503,"DOCUMENT_AUTHORITY_UNAVAILABLE","Trusted document authority is not configured.");
}

// PDF and document HTTP share the same server identity/read boundary.
public sealed class DocumentV2PdfAuthority(IDocumentV2Authority authority) : IPdfAuthority
{
    public async Task<V2EditorActor> EditorAsync(Guid d,Guid u,CancellationToken ct){var a=(await authority.MutateAsync(u,d,null,ct)).Actor;V2HttpActor.Verify(a,u);return a;}
    public async Task<bool> CanReadAsync(Guid d,Guid u,CancellationToken ct){var a=await authority.ReadAsync(u,ct);V2HttpActor.Verify(a.Actor,u);return a.ReadableDocumentIds.Contains(d);}
}
internal static class V2HttpActor
{
    public static void Verify(V2EditorActor actor,Guid user){if(actor.UserId!=user)throw new DocumentRegistrationRuleException(503,"AUTHORITY_MISMATCH","The authority response is invalid.");if(!actor.IsActive)throw new DocumentRegistrationRuleException(403,"ACTOR_INACTIVE","The actor is inactive.");}
}
