using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

/// <summary>Persists the allocation, document and its audit graph in one transaction.
/// EAP and authorization are not part of this writer; callers validate scope/references first.</summary>
public sealed class DocumentRegistrationWriter(DocumentDbContext db, TimeProvider clock)
{
    public Task<Document> RegisterLegacyAsync(Document document, CancellationToken token = default) =>
        RegisterAsync(document, (date, next) => DocumentNumberFormatter.FormatLegacy(document.DocType, date, next), token);

    public Task<Document> RegisterV2Async(Document document, string companyCode, string? departmentCode, CancellationToken token = default)
    {
        // Validate before opening a transaction or touching a counter.
        _ = DocumentNumberFormatter.Format(document.DocType, DocumentNumberFormatter.RegistrationDate(clock.GetUtcNow()), 1, companyCode, departmentCode);
        return RegisterAsync(document, (date, next) => DocumentNumberFormatter.Format(document.DocType, date, next, companyCode, departmentCode), token);
    }

    public Task<Document> RegisterIdempotentV2Async(Document document, string companyCode, string? departmentCode,
        RegistrationRequest request, CancellationToken token = default, V2RegistrationRelations? relations = null)
    {
        var header = document.Registration ?? throw new ArgumentException("A validated registration header is required.");
        if (header.DocumentId != document.Id || header.Kind != document.DocType || header.CompanyCode != companyCode ||
            header.InputterUserId != document.CreatedByUserId || header.LastModifierUserId != document.CreatedByUserId ||
            header.OriginatorUserId == Guid.Empty || header.OwnerDepartmentId == Guid.Empty || header.OwnerDepartmentId != document.SenderDepartmentId ||
            header.Version != 1 || document.Status != "InProgress" || document.SourceMessageId is not null ||
            document.Attachments.Count != 0 || request.Id == Guid.Empty || request.ActorUserId != document.CreatedByUserId ||
            request.Kind != document.DocType || !ValidHash(request.KeyHash) || !ValidHash(request.BodyHash) ||
            header.Sensitivity is not ("Normal" or "Confidential"))
            throw new ArgumentException("The initial v2 registration graph is invalid.");
        _ = DocumentNumberFormatter.Format("OUTGOING",new(2027,1,1),1,companyCode,header.OwnerDepartmentCodeSnapshot);
        if (document.DocType != "INCOMING" && departmentCode != header.OwnerDepartmentCodeSnapshot)
            throw new ArgumentException("The numbering department must match the registered owner.");
        _ = DocumentNumberFormatter.Format(document.DocType, DocumentNumberFormatter.RegistrationDate(clock.GetUtcNow()), 1, companyCode, departmentCode);
        if (relations is not null)
        {
            var ids = V2DocumentRelations.NormalizeRegistration(document.DocType, relations.RelatedDocumentIds);
            relations = ids is null ? null : new(ids, relations.Scope);
            if (relations is not null) V2DocumentRelations.ValidateScope(document.CreatedByUserId, relations.Scope, relations.RelatedDocumentIds);
        }
        return RegisterAsync(document, (date,next)=>DocumentNumberFormatter.Format(document.DocType,date,next,companyCode,departmentCode),token,request,relations);
    }

    private async Task<Document> RegisterAsync(Document document, Func<DateOnly, int, string> format, CancellationToken token,
        RegistrationRequest? request = null, V2RegistrationRelations? relations = null)
    {
        if (document.Registration is not null && request is null)
            throw new ArgumentException("V2 header persistence requires a registration receipt.");
        DocumentNumberFormatter.ValidateKind(document.DocType);
        document.SourceMessageId = string.IsNullOrWhiteSpace(document.SourceMessageId) ? null : document.SourceMessageId.Trim();
        if (document.SourceMessageId is not null && document.DocType != DocumentTypeConstants.INCOMING)
            throw new ArgumentException("Only incoming registrations can have a source message ID.");
        if (document.Id == Guid.Empty || string.IsNullOrWhiteSpace(document.Title) || document.CreatedByUserId == Guid.Empty)
            throw new ArgumentException("Registration requires an ID, title and actor.");
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Registration must own a clean unit of work.");
        var relational = db.Database.IsSqlServer() || db.Database.IsSqlite();
        if (relations is not null && !relational) throw new NotSupportedException("Relations require a relational database.");
        if (!relational && db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new NotSupportedException("Unsupported registration database provider.");

        if (document.StatusHistories.Count == 0)
            document.StatusHistories.Add(new DocumentStatusHistory
            {
                DocumentId = document.Id,
                OldStatus = null,
                NewStatus = document.Status,
                ChangedByUserId = document.CreatedByUserId,
                Note = "Registration"
            });
        if (document.StatusHistories.Count != 1 || document.StatusHistories.Any(h =>
                h.DocumentId != document.Id || h.ChangedByUserId != document.CreatedByUserId ||
                h.OldStatus is not null || h.NewStatus != document.Status))
            throw new ArgumentException("Registration requires exactly one initial audit matching the document and actor.");

        // One server instant for date/number/audit, including transaction retries.
        var registeredAt = clock.GetUtcNow();
        var date = DocumentNumberFormatter.RegistrationDate(registeredAt);
        document.CreatedAt = registeredAt.UtcDateTime;
        if (document.Registration is { } header)
        {
            header.RegistrationDate = date; header.RegistrationYear = date.Year; header.RegisteredAt = registeredAt;
        }
        DocumentOutboxEvent? registeredEvent = null;
        if (request is not null)
        {
            request.DocumentId = document.Id; request.CreatedAt = registeredAt;
            registeredEvent = new() { DocumentId=document.Id, CreatedAt=registeredAt,
                PayloadJson=JsonSerializer.Serialize(new { documentId=document.Id, version=1 }) };
        }
        if (document.DocType == DocumentTypeConstants.INCOMING) document.ReceivedAt ??= document.CreatedAt;
        foreach (var h in document.StatusHistories) h.ChangedAt = document.CreatedAt;
        foreach (var a in document.Attachments) a.CreatedAt = document.CreatedAt;
        var attempts = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                token.ThrowIfCancellationRequested();
                ++attempts;
                db.ChangeTracker.Clear();
                await using var transaction = relational ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
                try
                {
                    if (relations is not null) await V2MutationLocks.RelationsAsync(db, token);
                    if (request is not null)
                    {
                        if (db.Database.IsSqlServer())
                        {
                            var receiptKey = $"das:registration:{request.ActorUserId:N}:{request.Kind}:{request.KeyHash}";
                            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={receiptKey}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; DECLARE @m nvarchar(2048)=CONCAT('Cannot acquire registration replay lock; result=',@r); IF @r < 0 THROW 51002, @m, 1;", token);
                        }
                    }
                    if (db.Database.IsSqlServer())
                    {
                        var key = $"das:document-number:{document.DocType}:{date.Year}";
                        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={key}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; DECLARE @m nvarchar(2048)=CONCAT('Cannot acquire document allocation lock; result=',@r); IF @r < 0 THROW 51002, @m, 1;", token);
                    }
                    if (request is not null)
                    {
                        // Acquire application locks before reading missing receipt keys. Shared
                        // serializable range locks held by counter waiters otherwise block INSERT.
                        // Update-range locking also protects independent keys across different years.
                        var replayedRequest = await RegistrationReplayAsync(request,token,lockReceipt:true);
                        if (replayedRequest is not null) return replayedRequest;
                    }
                    if (attempts > 1)
                    {
                        // A transient commit acknowledgement failure may follow a successful commit.
                        var committed = await StoredAsync(document.Id, token);
                        if (committed is not null) return committed;
                    }
                    var replay = await SourceReplayAsync(document, token);
                    if (replay is not null) return replay;

                    var counter = db.Database.IsSqlServer()
                        ? await db.DocumentNumberCounters.FromSqlInterpolated($"SELECT * FROM document.DocumentNumberCounters WITH (UPDLOCK, HOLDLOCK) WHERE DocType={document.DocType} AND Year={date.Year}").SingleOrDefaultAsync(token)
                        : await db.DocumentNumberCounters.SingleOrDefaultAsync(x => x.DocType == document.DocType && x.Year == date.Year, token);
                    if (counter is null)
                    {
                        counter = new() { DocType = document.DocType, Year = date.Year, CurrentValue = 0 };
                        db.DocumentNumberCounters.Add(counter);
                    }
                    if (counter.CurrentValue < 0) throw new InvalidOperationException("The persisted counter is invalid.");
                    counter.CurrentValue = checked(counter.CurrentValue + 1);
                    document.DocumentNumber = format(date, counter.CurrentValue);
                    if (document.Registration is { } registration) registration.SequenceNumber = counter.CurrentValue;
                    db.Documents.Add(document);
                    if (relations is not null)
                    {
                        var planner = new V2DocumentRelations(db);
                        var change = new V2RelationChange(relations.RelatedDocumentIds, Array.Empty<Guid>());
                        var plan = await planner.PrepareAsync(document, change, document.CreatedByUserId, relations.Scope, true, token);
                        planner.Apply(plan, document.CreatedByUserId, registeredAt);
                    }
                    if (request is not null)
                    {
                        db.RegistrationRequests.Add(request);
                        db.DocumentOutboxEvents.Add(registeredEvent!);
                    }
                    await db.SaveChangesAsync(token);
                    if (transaction is not null) await transaction.CommitAsync(token);
                    return document;
                }
                catch
                {
                    // Transaction disposal rolls back the relational write. Never fall back to LINQ
                    // after a database error, or retain Added entities for the next request.
                    db.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e) && (document.SourceMessageId is not null || request is not null))
        {
            // Same source can race across two registration years. Unique SourceMessageId is final authority.
            db.ChangeTracker.Clear();
            if (request is not null)
            {
                var registeredRequest = await RegistrationReplayAsync(request,token);
                if (registeredRequest is not null) return registeredRequest;
            }
            var replay = await SourceReplayAsync(document, token);
            if (replay is not null) return replay;
            throw;
        }
    }

    private Task<Document?> StoredAsync(Guid id, CancellationToken token) =>
        db.Documents.IgnoreQueryFilters().AsNoTracking().Include(x => x.Registration).Include(x => x.KindDetails).Include(x => x.Recipients).Include(x => x.Attachments).Include(x => x.DepartmentAccesses).Include(x => x.StatusHistories)
            .SingleOrDefaultAsync(x => x.Id == id, token);

    internal async Task<Document?> RegistrationReplayAsync(RegistrationRequest request, CancellationToken token, bool lockReceipt=false)
    {
        var query = lockReceipt && db.Database.IsSqlServer()
            ? db.RegistrationRequests.FromSqlInterpolated($"SELECT * FROM document.RegistrationRequests WITH (UPDLOCK, HOLDLOCK) WHERE ActorUserId={request.ActorUserId} AND Kind={request.Kind} AND KeyHash={request.KeyHash}")
            : db.RegistrationRequests.Where(x=>x.ActorUserId==request.ActorUserId && x.Kind==request.Kind && x.KeyHash==request.KeyHash);
        var stored = await query.AsNoTracking().SingleOrDefaultAsync(token);
        if (stored is null) return null;
        if (!string.Equals(stored.BodyHash,request.BodyHash,StringComparison.Ordinal))
            throw new DocumentRegistrationRuleException(409,"IDEMPOTENCY_CONFLICT","This registration key has already been used for different data.");
        var document = await StoredAsync(stored.DocumentId,token);
        if (document?.IsDeleted == true || document?.Registration is null)
            throw new DocumentRegistrationRuleException(409,"REGISTRATION_UNAVAILABLE","The original registration is unavailable; its key cannot be reused.");
        return document;
    }

    private async Task<Document?> SourceReplayAsync(Document document, CancellationToken token)
    {
        if (document.SourceMessageId is null) return null;
        var replay = await db.Documents.IgnoreQueryFilters().AsNoTracking().Include(x => x.Registration).Include(x => x.Attachments).Include(x => x.DepartmentAccesses).Include(x => x.StatusHistories)
            .SingleOrDefaultAsync(x => x.SourceMessageId == document.SourceMessageId, token);
        if (replay?.Registration is not null && document.Registration is null)
            throw new InvalidOperationException("A v2 registration cannot be replayed through the legacy source path.");
        if (replay?.IsDeleted == true) throw new InvalidOperationException("This source message belongs to an existing deleted registration.");
        return replay;
    }

    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is SqlException { Number: 2601 or 2627 } || e.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 };

    private static bool ValidHash(string? hash) => hash?.Length==64 && hash.All(c=>char.IsAsciiHexDigit(c));
}
