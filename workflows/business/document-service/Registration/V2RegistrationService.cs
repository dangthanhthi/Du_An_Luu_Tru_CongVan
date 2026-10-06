using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public sealed class V2RegistrationService(DocumentDbContext db, TimeProvider clock)
{
    public async Task<Document> RegisterAsync(V2RegistrationDraft draft, RegistrationIdentity identity,
        string idempotencyKey, CancellationToken ct = default, V2ReferenceSet? references = null, V2RelationScope? relationScope = null)
    {
        if (identity.InputterUserId == Guid.Empty) throw Rule(401,"ACTOR_REQUIRED","A verified actor is required.");
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Registration must own a clean unit of work.");
        if (idempotencyKey is null || !Regex.IsMatch(idempotencyKey, @"\A[A-Za-z0-9._:-]{1,128}\z", RegexOptions.CultureInvariant))
            throw Rule(400,"INVALID_IDEMPOTENCY_KEY","A valid registration key is required.");
        DocumentNumberFormatter.ValidateKind(draft.Kind);
        if (string.IsNullOrWhiteSpace(draft.Subject) || draft.Subject.Trim().Length > 2000 || draft.Remark?.Length > 4000 ||
            draft.OriginatorUserId == Guid.Empty || draft.OwnerDepartmentId == Guid.Empty ||
            draft.Sensitivity is not ("Normal" or "Confidential") || string.IsNullOrWhiteSpace(draft.CompanyCode))
            throw Rule(400,"INVALID_REGISTRATION","The registration draft is invalid.");
        draft = draft with { Subject = draft.Subject.Trim(), CompanyCode = draft.CompanyCode.Trim().ToUpperInvariant(),
            Remark = string.IsNullOrWhiteSpace(draft.Remark) ? null : draft.Remark.Trim(),
            Details = draft.Details is null ? null : V2KindDetailsMapper.Normalize(draft.Kind, draft.Details),
            RelatedDocumentIds = V2DocumentRelations.NormalizeRegistration(draft.Kind, draft.RelatedDocumentIds) };
        if (draft.RelatedDocumentIds is not null) V2DocumentRelations.ValidateScope(identity.InputterUserId, relationScope, draft.RelatedDocumentIds);
        var receipt = new RegistrationRequest { ActorUserId=identity.InputterUserId, Kind=draft.Kind,
            KeyHash=Hash(idempotencyKey), BodyHash=Hash(JsonSerializer.Serialize(draft)) };
        var writer = new DocumentRegistrationWriter(db,clock);
        // Existing receipt is authoritative even if live reference names changed.
        // Authorization/resource policy belongs to the trusted caller, also on replays.
        var replay = await writer.RegistrationReplayAsync(receipt,ct);
        if (replay is not null) return replay;
        if (identity.OriginatorUserId != draft.OriginatorUserId || identity.OwnerDepartmentId != draft.OwnerDepartmentId ||
            string.IsNullOrWhiteSpace(identity.OwnerDepartmentName) || identity.OwnerDepartmentName.Trim().Length > 200)
            throw Rule(400,"INVALID_REGISTRATION_IDENTITY","The verified originator and owner do not match the draft.");
        var company = await db.BusinessCatalogEntries.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Group=="companies" && x.Code==draft.CompanyCode && x.IsActive,ct)
            ?? throw Rule(400,"INVALID_COMPANY","An active DAS company is required.");
        _ = DocumentNumberFormatter.Format("OUTGOING",new(2027,1,1),1,draft.CompanyCode,identity.OwnerDepartmentCode);
        var document = new Document { DocType=draft.Kind, Status="InProgress", Title=draft.Subject,
            CreatedByUserId=identity.InputterUserId, SenderDepartmentId=identity.OwnerDepartmentId };
        document.Registration = new() { DocumentId=document.Id, Kind=draft.Kind, CompanyCode=company.Code,
            CompanyNameSnapshot=company.Name, OwnerDepartmentId=identity.OwnerDepartmentId,
            OwnerDepartmentCodeSnapshot=identity.OwnerDepartmentCode.Trim().ToUpperInvariant(),
            OwnerDepartmentNameSnapshot=identity.OwnerDepartmentName.Trim(), InputterUserId=identity.InputterUserId,
            OriginatorUserId=identity.OriginatorUserId, LastModifierUserId=identity.InputterUserId,
            Sensitivity=draft.Sensitivity, IssuedDate=draft.IssuedDate, Remark=draft.Remark };
        if (draft.Details is not null)
        {
            var mapper = new V2KindDetailsMapper(db);
            mapper.Apply(document, await mapper.PrepareAsync(document, draft.Details, references, ct));
        }
        return await writer.RegisterIdempotentV2Async(document,company.Code,
            draft.Kind=="INCOMING" ? null : document.Registration.OwnerDepartmentCodeSnapshot,receipt,ct,
            draft.RelatedDocumentIds is null ? null : new V2RegistrationRelations(draft.RelatedDocumentIds, relationScope!));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static DocumentRegistrationRuleException Rule(int status,string code,string message)=>new(status,code,message);
}
