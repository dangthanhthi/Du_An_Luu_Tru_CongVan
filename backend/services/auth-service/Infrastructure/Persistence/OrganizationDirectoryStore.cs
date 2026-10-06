using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthService.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AuthService;

// Trusted normalized adapter only; no public snapshot-writing endpoint.
public sealed class OrganizationDirectoryStore(AuthDbContext db, TimeProvider clock)
{
    private const int MaxPayloadBytes = 1024 * 1024;
    public async Task<DirectorySyncResult> ApplyAsync(string sourceId, string messageId,
        OrganizationDirectorySnapshot snapshot, CancellationToken ct = default)
    {
        ValidateSource(sourceId);
        if (string.IsNullOrWhiteSpace(messageId) || messageId.Length > 128 || messageId != messageId.Trim())
            throw new ArgumentException("A stable message ID of at most 128 characters is required.", nameof(messageId));
        ArgumentNullException.ThrowIfNull(snapshot);
        var wire = JsonSerializer.Serialize(snapshot);
        if (Encoding.UTF8.GetByteCount(wire) > MaxPayloadBytes)
            throw new ArgumentException("Normalized directory snapshot exceeds supported size.", nameof(snapshot));
        var initial = OrganizationProjectionPlanner.Prepare(null, snapshot);
        var canonical = initial.Projection is null ? wire : Serialize(initial.Projection);
        // Includes sequence; reusing the same message ID for another sequence conflicts.
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    if (db.Database.IsSqlServer())
                    {
                        // One writer per normalized source. Transaction-owned locks also
                        // serialize first-head creation; CAS remains a second safeguard.
                        var resource = "DAS.Directory." + sourceId;
                        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Directory writer lock unavailable.', 1;", ct);
                    }
                    var receipt = await db.DirectoryInbox.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.SourceId == sourceId && x.MessageId == messageId, ct);
                    if (receipt is not null)
                    {
                        var recorded = JsonSerializer.Deserialize<DirectorySyncResult>(receipt.Result)
                            ?? throw new DirectoryUnavailableException("DIRECTORY_RECEIPT_INVALID");
                        await transaction.CommitAsync(ct);
                        return receipt.PayloadHash == digest ? recorded with { IsDuplicate = true }
                            : new DirectorySyncResult(DirectorySyncDisposition.MessageConflict,
                                recorded.Sequence, recorded.AuthorizationRevision, [new("MESSAGE_ID_CONFLICT", null)]);
                    }
                    var head = await db.DirectoryProjections.SingleOrDefaultAsync(x => x.SourceId == sourceId, ct);
                    var current = head is null ? null : Decode(head);
                    var preparation = initial.Disposition == ProjectionDisposition.Rejected ? initial
                        : OrganizationProjectionPlanner.Prepare(current, snapshot);
                    var revision = head?.AuthorizationRevision ?? 0;
                    DirectorySyncResult result;
                    if (preparation.Disposition == ProjectionDisposition.Prepared)
                    {
                        var projection = preparation.Projection!;
                        revision = checked(revision + 1);
                        head ??= new DirectoryProjectionState { SourceId = sourceId };
                        if (db.Entry(head).State == EntityState.Detached) db.DirectoryProjections.Add(head);
                        head.Sequence = projection.Sequence;
                        head.AuthorizationRevision = revision;
                        head.Fingerprint = projection.Fingerprint;
                        head.Payload = Serialize(projection);
                        head.VerifiedAt = clock.GetUtcNow();
                        db.DirectoryOutbox.Add(new DirectoryOutboxEvent
                        {
                            SourceId = sourceId, Sequence = head.Sequence,
                            AuthorizationRevision = revision, CreatedAt = head.VerifiedAt
                        });
                        result = new(DirectorySyncDisposition.Applied, head.Sequence, revision, []);
                    }
                    else result = new(preparation.Disposition == ProjectionDisposition.Rejected
                        ? DirectorySyncDisposition.Rejected : DirectorySyncDisposition.Unchanged,
                        head?.Sequence ?? 0, revision, preparation.Errors);
                    db.DirectoryInbox.Add(new DirectoryInboxReceipt
                    {
                        SourceId = sourceId, MessageId = messageId, PayloadHash = digest,
                        Result = JsonSerializer.Serialize(result), ReceivedAt = clock.GetUtcNow()
                    });
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return result;
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3) { }
            catch (DbUpdateException e) when (attempt < 3 && IsUniqueConflict(e)) { }
            // EF retries SQL transient errors around the entire transaction, then
            // re-reads the committed inbox if a commit had an unknown outcome.
        }
    }
    public async Task<PersistedDirectory> ReadFreshAsync(string sourceId, TimeSpan maxAge, CancellationToken ct = default)
    {
        ValidateSource(sourceId);
        if (maxAge <= TimeSpan.Zero || maxAge > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        var head = await db.DirectoryProjections.AsNoTracking().SingleOrDefaultAsync(x => x.SourceId == sourceId, ct)
            ?? throw new DirectoryUnavailableException("DIRECTORY_NOT_READY");
        var age = clock.GetUtcNow() - head.VerifiedAt;
        if (age < TimeSpan.Zero) throw new DirectoryUnavailableException("DIRECTORY_CLOCK_INVALID");
        if (age > maxAge) throw new DirectoryUnavailableException("DIRECTORY_STALE");
        return new(Decode(head), head.AuthorizationRevision, head.VerifiedAt);
    }
    private static PreparedOrganizationDirectory Decode(DirectoryProjectionState head)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<OrganizationDirectorySnapshot>(head.Payload);
            if (snapshot is null) throw new DirectoryUnavailableException("DIRECTORY_CORRUPT");
            var prepared = OrganizationProjectionPlanner.Prepare(null, snapshot);
            if (prepared.Projection is null || prepared.Projection.Sequence != head.Sequence ||
                prepared.Projection.Fingerprint != head.Fingerprint)
                throw new DirectoryUnavailableException("DIRECTORY_CORRUPT");
            return prepared.Projection;
        }
        catch (JsonException) { throw new DirectoryUnavailableException("DIRECTORY_CORRUPT"); }
    }
    private static string Serialize(PreparedOrganizationDirectory projection) =>
        JsonSerializer.Serialize(new OrganizationDirectorySnapshot(projection.Sequence,
            projection.Units, projection.Users, projection.Memberships, projection.Leadership));
    private static void ValidateSource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 64 ||
            sourceId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.'))
            throw new ArgumentException("A bounded source ID is required.", nameof(sourceId));
    }
    private static bool IsUniqueConflict(DbUpdateException e) =>
        e.InnerException is SqlException { Number: 2601 or 2627 } ||
        e.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 };
}

