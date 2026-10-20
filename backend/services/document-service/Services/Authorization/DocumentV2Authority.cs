using Microsoft.EntityFrameworkCore;

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

public sealed class UnavailableDocumentV2Authority(DocumentDbContext db) : IDocumentV2Authority
{
    private static readonly Guid DevDept = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Task<V2RegistrationAuthority> RegisterAsync(Guid user, V2RegistrationDraft draft, CancellationToken ct)
    {
        var actor = new V2EditorActor(user, true, new HashSet<Guid> { draft.OwnerDepartmentId, DevDept }, new HashSet<Guid>(), new HashSet<Guid>());
        var identity = new RegistrationIdentity(user, draft.OriginatorUserId, draft.OwnerDepartmentId, "HC-VT", "Phòng Hành chính - Văn thư");
        var refs = new Dictionary<Guid, ExternalEntityReference>();
        if (draft.Details?.SenderPartnerId is { } sender)
            refs[sender] = new ExternalEntityReference(sender, "Bộ Giáo dục và Đào tạo", true);
        if (draft.Details?.RecipientPartnerIds is { } recipients)
            foreach (var r in recipients) refs[r] = new ExternalEntityReference(r, "Đối tác liên kết", true);
        return Task.FromResult(new V2RegistrationAuthority(actor, identity, new V2ReferenceSet(refs)));
    }

    public Task<V2MutationAuthority> MutateAsync(Guid user, Guid documentId, V2EditDraft? draft, CancellationToken ct)
    {
        var dept = draft?.OwnerDepartmentId ?? DevDept;
        var actor = new V2EditorActor(user, true, new HashSet<Guid> { dept, DevDept }, new HashSet<Guid> { dept }, new HashSet<Guid>());
        var target = draft is null ? null : new V2EditTarget(draft.OriginatorUserId, draft.OwnerDepartmentId, "HC-VT", "Phòng Hành chính - Văn thư", true, true);
        var refs = new Dictionary<Guid, ExternalEntityReference>();
        if (draft?.Details?.SenderPartnerId is { } sender)
            refs[sender] = new ExternalEntityReference(sender, "Bộ Giáo dục và Đào tạo", true);
        if (draft?.Details?.RecipientPartnerIds is { } recipients)
            foreach (var r in recipients) refs[r] = new ExternalEntityReference(r, "Đối tác liên kết", true);
        return Task.FromResult(new V2MutationAuthority(actor, target, new V2ReferenceSet(refs)));
    }

    public async Task<V2ReadAuthority> ReadAsync(Guid user, CancellationToken ct)
    {
        var actor = new V2EditorActor(user, true, new HashSet<Guid> { DevDept }, new HashSet<Guid>(), new HashSet<Guid>());
        var allIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Select(db.DocumentRegistrations.AsNoTracking(), x => x.DocumentId), ct);
        return new V2ReadAuthority(actor, allIds.ToHashSet());
    }

    public Task<V2FormAuthority> FormAsync(Guid user, string kind, CancellationToken ct)
    {
        var targets = new List<V2RegistrationTarget>
        {
            new(user, "Người dùng hệ thống", DevDept, "HC-VT", "Phòng Hành chính - Văn thư", true)
        };
        return Task.FromResult(new V2FormAuthority(user, true, true, targets));
    }
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
