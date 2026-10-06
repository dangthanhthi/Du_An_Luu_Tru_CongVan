extern alias doc;
extern alias files;
extern alias auth;
extern alias partner;
using D = doc::DocumentService;
using F = files::FilesService;
using A = auth::AuthService;
using P = partner::PartnerService;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace RestoreIntegration.Tests;

public sealed class SqlRestoreDrillTests
{
    [RestoreSqlFact]
    public async Task Five_real_SQL_backups_preserve_revocations_tombstones_data_and_PDF_without_replaying_unknown_SMTP()
    {
        var timer = Stopwatch.StartNew();
        await using var f = await Fixture.Create();
        var actor = Guid.NewGuid(); var department = Guid.NewGuid(); var otherDepartment = Guid.NewGuid();
        var identity = new D.RegistrationIdentity(actor, actor, department, "ADM", "Administration");
        var firstDraft = new D.V2RegistrationDraft("INTERNAL", "HL", "Synthetic restore fixture", actor, department, IssuedDate: new(2026, 12, 1));
        var clock = new Clock();
        var revokedAt = clock.GetUtcNow().UtcDateTime.AddDays(-1);
        var revokedToken = new string('d', 64); // Synthetic stored hash, never a usable credential.
        var roleId = Guid.NewGuid();
        var projectionPayload = "{\"fixture\":true,\"noAuthorityGranted\":true}";
        var directoryEvent = Guid.NewGuid();
        await using (var db = f.Auth())
        {
            db.Departments.Add(new A.Department { Id = department, Code = "ADM", Name = "Synthetic Administration", CreatedAt = revokedAt });
            db.Roles.Add(new A.Role { Id = roleId, Name = "SyntheticReadOnly" });
            db.Users.Add(new A.User { Id = actor, Username = "synthetic-inactive", PasswordHash = "!synthetic-unusable-password", FullName = "Inactive restore fixture", Email = "inactive@example.test", DepartmentId = department, IsActive = false, CreatedAt = revokedAt });
            db.UserRoles.Add(new A.UserRole { UserId = actor, RoleId = roleId });
            db.RefreshTokens.Add(new A.RefreshToken { UserId = actor, Token = revokedToken, CreatedAt = revokedAt.AddDays(-1), ExpiresAt = revokedAt.AddDays(30), RevokedAt = revokedAt });
            // Existing persistence only: no EAP client, synchronization or authorization projection execution.
            db.DirectoryProjections.Add(new A.Organization.DirectoryProjectionState { SourceId = "synthetic", Sequence = 7, AuthorizationRevision = 3, Fingerprint = new string('e', 64), Payload = projectionPayload, VerifiedAt = clock.GetUtcNow() });
            db.DirectoryInbox.Add(new A.Organization.DirectoryInboxReceipt { SourceId = "synthetic", MessageId = "synthetic-message", PayloadHash = new string('f', 64), Result = "Applied", ReceivedAt = clock.GetUtcNow() });
            db.DirectoryOutbox.Add(new A.Organization.DirectoryOutboxEvent { Id = directoryEvent, SourceId = "synthetic", Sequence = 7, AuthorizationRevision = 3, CreatedAt = clock.GetUtcNow(), PublishedAt = null });
            await db.SaveChangesAsync();
        }
        var activePartner = Guid.NewGuid(); var deletedPartner = Guid.NewGuid();
        await using (var db = f.Partner())
        {
            db.Partners.Add(new P.Partner { Id = activePartner, FullName = "Synthetic Active Entity", ShortName = "QA-A", NormalizedShortName = "QA-A", EntityType = "Both", ContactPerson = "Synthetic Contact", ContactInformation = "fixture@example.test", CreatedByUserId = actor, CreatedAt = revokedAt, Version = 2 });
            db.Partners.Add(new P.Partner { Id = deletedPartner, FullName = "Synthetic Deleted Entity", ShortName = "QA-D", NormalizedShortName = "QA-D", EntityType = "Sender", IsActive = false, IsDeleted = true, DeletedAt = revokedAt, CreatedByUserId = actor, CreatedAt = revokedAt.AddDays(-1), Version = 3 });
            db.PartnerAudits.Add(new P.PartnerAudit { PartnerId = activePartner, ActorUserId = actor, Action = "Updated", Version = 2, CreatedAt = revokedAt });
            db.PartnerAudits.Add(new P.PartnerAudit { PartnerId = deletedPartner, ActorUserId = actor, Action = "Deleted", Version = 3, CreatedAt = revokedAt });
            await db.SaveChangesAsync();
        }
        Guid document; string originalNumber;
        await using (var db = f.Document())
        {
            var service = new D.V2RegistrationService(db, clock);
            var first = await service.RegisterAsync(firstDraft, identity, "restore-first");
            document = first.Id; Assert.Equal("27-01-0001/INT/HL/ADM", first.DocumentNumber);
            var editor = new D.V2EditorActor(actor, true, new HashSet<Guid> { department, otherDepartment }, new HashSet<Guid>(), new HashSet<Guid>());
            var edited = await new D.V2DocumentEditor(db, clock).UpdateAsync(document,
                new(1, "HV", firstDraft.Subject, actor, otherDepartment, "Normal", firstDraft.IssuedDate, null), editor,
                new(actor, otherDepartment, "FIN", "Finance", true, true));
            originalNumber = edited.DocumentNumber; Assert.Equal("27-01-0001/INT/HV/FIN", originalNumber);
            await new D.V2DocumentLifecycle(db, clock).ChangeAsync(document, new(edited.Registration!.Version, D.V2StatusAction.Cancel, "Synthetic cancel"), editor);
            var cancelled = await db.DocumentRegistrations.SingleAsync();
            await new D.V2DocumentLifecycle(db, clock).ChangeAsync(document, new(cancelled.Version, D.V2StatusAction.Restore), editor);
            Assert.Equal("InProgress", (await db.Documents.SingleAsync()).Status);
            // Synthetic high-water fixture: no need to insert 9,998 unrelated documents.
            await db.DocumentNumberCounters.ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentValue, 9999));
            db.ChangeTracker.Clear();
            var rare = await service.RegisterAsync(firstDraft with { Subject = "Rare overflow" }, identity, "restore-overflow");
            Assert.Equal("27-01-10000/INT/HL/ADM", rare.DocumentNumber);
        }

        var fileId = Guid.NewGuid(); var operation = Guid.NewGuid();
        var pdf = new PdfDocumentBuilder(); pdf.AddPage(UglyToad.PdfPig.Content.PageSize.A4); var bytes = pdf.Build();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var sourceStorage = Path.Combine(f.TempRoot, "source-storage"); Directory.CreateDirectory(sourceStorage);
        var fileName = fileId.ToString("N") + ".pdf"; var sourcePath = Path.Combine(sourceStorage, fileName);
        await File.WriteAllBytesAsync(sourcePath, bytes);
        await using (var db = f.Files())
        {
            db.Files.Add(new F.Models.Entities.FileRecord { Id = fileId, OriginalName = "synthetic.pdf", StoragePath = sourcePath, ContentType = "application/pdf", SizeBytes = bytes.Length, UploadedByUserId = actor, CreatedAt = clock.GetUtcNow().UtcDateTime });
            db.Add(new F.Models.Entities.PdfUpload { FileId = fileId, UploaderUserId = actor, StorageKey = fileName, OriginalName = "synthetic.pdf", State = "Available", SizeBytes = bytes.Length, Sha256 = hash, DocumentId = document, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() });
            db.Add(new F.Models.Entities.PdfClaim { OperationId = operation, FileId = fileId, DocumentId = document, UploaderUserId = actor, ExpectedVersion = 3, State = "Active", CreatedAt = clock.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync();
        }
        var sender = Guid.NewGuid(); var key = Guid.NewGuid().ToString("N");
        var message = new DurableMessage(actor, "restore@example.test", "Synthetic reminder", "Synthetic body", null, "Warning", null);
        Guid receipt; Guid sending; Guid unknown; Guid sent; Guid dead;
        await using (var db = f.Notification())
        {
            var inbox = new DurableNotifications(db);
            receipt = (await inbox.AcceptAsync(sender, key, message, default)).Id;
            sending = (await inbox.AcceptAsync(sender, "sending", message, default)).Id;
            unknown = (await inbox.AcceptAsync(sender, "unknown", message, default)).Id;
            sent = (await inbox.AcceptAsync(sender, "sent", message, default)).Id;
            dead = (await inbox.AcceptAsync(sender, "dead", message, default)).Id;
            foreach (var row in await db.Set<DeliveryInbox>().ToListAsync())
            {
                if (row.Id == sending) { row.State = "Sending"; row.LeaseUntilUnix = 1; row.Attempts = 1; }
                if (row.Id == unknown) row.State = "UnknownOutcome";
                if (row.Id == sent) row.State = "Sent";
                if (row.Id == dead) { row.State = "DeadLetter"; row.Attempts = 5; }
            }
            await db.SaveChangesAsync();
        }
        var task = new D.DocumentTaskIntent { DocumentId = document, ActorId = actor, AssigneeId = actor, Title = "Synthetic unknown task", KeyHash = new string('a', 64), BodyHash = new string('b', 64), State = "UnknownOutcome", RemoteTaskId = "synthetic-known-correlation" };
        var batch = new D.ReminderBatch { DepartmentId = otherDepartment, Period = new(2027, 1, 4), State = "Queued", CreatedAt = clock.GetUtcNow(), PayloadJson = "synthetic-frozen-plan" };
        var delivery = new D.ReminderDelivery { BatchId = batch.Id, InputterUserId = actor, State = "Accepted", PayloadJson = "synthetic-frozen-body", NotificationId = receipt, NotificationState = "Queued", Attempts = 1 };
        await using (var db = f.Document())
        {
            db.Add(new D.DocumentCurrentPdf { DocumentId = document, FileId = fileId, OperationId = operation, OriginalName = "synthetic.pdf", SizeBytes = bytes.Length, Sha256 = hash, State = "Ready" });
            db.Add(new D.PdfReplacement { OperationId = operation, DocumentId = document, FileId = fileId, ActorUserId = actor, ExpectedVersion = 3, CommittedVersion = 4, State = "Committed", CreatedAt = clock.GetUtcNow().UtcDateTime });
            db.Add(task); db.Add(batch); db.Add(new D.ReminderFanoutManifest { BatchId = batch.Id, PlanHash = new string('c', 64) }); db.Add(delivery);
            var change = await db.DocumentOutboxEvents.FirstAsync();
            db.Add(new D.DocumentNotificationDelivery { EventId = change.Id, RecipientId = actor, State = "Accepted", PayloadJson = "synthetic-persisted-body", Attempts = 1 });
            await db.SaveChangesAsync();
        }

        // No hosts/workers exist in this fixture. All writes now stop until the complete backup set exists.
        var before = new Dictionary<string, string>(); var after = new Dictionary<string, string>();
        foreach (var component in Fixture.Components)
        {
            before[component] = await f.DataHash(component, restored: false);
            await f.BackupAndRestore(component);
            after[component] = await f.DataHash(component, restored: true);
            Assert.Equal(before[component], after[component]);
        }
        var restoredStorage = Path.Combine(f.TempRoot, "restored-storage"); Directory.CreateDirectory(restoredStorage);
        File.Copy(sourcePath, Path.Combine(restoredStorage, fileName));
        var restoredBytes = await File.ReadAllBytesAsync(Path.Combine(restoredStorage, fileName));
        Assert.Equal(bytes, restoredBytes);
        await using (var db = f.Auth(restored: true))
        {
            var user = await db.Users.AsNoTracking().SingleAsync();
            Assert.Equal(actor, user.Id); Assert.False(user.IsActive); Assert.Equal(department, user.DepartmentId);
            Assert.Equal("!synthetic-unusable-password", user.PasswordHash);
            var token = await db.RefreshTokens.AsNoTracking().SingleAsync();
            Assert.Equal(actor, token.UserId); Assert.Equal(revokedToken, token.Token); Assert.Equal(revokedAt, token.RevokedAt);
            Assert.Equal(roleId, (await db.UserRoles.AsNoTracking().SingleAsync()).RoleId);
            var projection = await db.DirectoryProjections.AsNoTracking().SingleAsync();
            Assert.Equal(7, projection.Sequence); Assert.Equal(3, projection.AuthorizationRevision); Assert.Equal(projectionPayload, projection.Payload);
            Assert.Equal("synthetic-message", (await db.DirectoryInbox.AsNoTracking().SingleAsync()).MessageId);
            var pending = await db.DirectoryOutbox.AsNoTracking().SingleAsync();
            Assert.Equal(directoryEvent, pending.Id); Assert.Null(pending.PublishedAt); Assert.Equal(3, pending.AuthorizationRevision);
        }
        await using (var db = f.Partner(restored: true))
        {
            var visible = await db.Partners.AsNoTracking().SingleAsync();
            Assert.Equal(activePartner, visible.Id); Assert.Equal(2, visible.Version);
            Assert.Equal("Synthetic Contact", visible.ContactPerson); Assert.Equal("fixture@example.test", visible.ContactInformation);
            var deleted = await db.Partners.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == deletedPartner);
            Assert.True(deleted.IsDeleted); Assert.False(deleted.IsActive); Assert.Equal(revokedAt, deleted.DeletedAt); Assert.Equal(3, deleted.Version);
            Assert.Equal(2, await db.Partners.IgnoreQueryFilters().CountAsync());
            Assert.Equal("Deleted", (await db.PartnerAudits.SingleAsync(x => x.PartnerId == deletedPartner)).Action);
            Assert.Equal(2, await db.PartnerAudits.CountAsync());
        }
        await using (var db = f.Document(restored: true))
        {
            var header = await db.DocumentRegistrations.SingleAsync(x => x.DocumentId == document);
            Assert.Equal(1, header.SequenceNumber); Assert.Equal("HV", header.CompanyCode); Assert.Equal(otherDepartment, header.OwnerDepartmentId);
            Assert.Equal(originalNumber, (await db.Documents.SingleAsync(x => x.Id == document)).DocumentNumber);
            Assert.Equal(10000, (await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
            Assert.Equal(task.Id, (await db.Set<D.DocumentTaskIntent>().SingleAsync()).Id);
            Assert.Equal("UnknownOutcome", (await db.Set<D.DocumentTaskIntent>().SingleAsync()).State);
            var current = await db.Set<D.DocumentCurrentPdf>().SingleAsync(); Assert.Equal(hash, current.Sha256); Assert.Equal(fileId, current.FileId);
            Assert.Equal(receipt, (await db.Set<D.ReminderDelivery>().SingleAsync()).NotificationId);
            // Same idempotency key must replay the old registration, without advancing the restored counter.
            var replay = await new D.V2RegistrationService(db, clock).RegisterAsync(firstDraft, identity, "restore-first");
            Assert.Equal(document, replay.Id); Assert.Equal(10000, (await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        }
        await using (var db = f.Files(restored: true))
        {
            var upload = await db.Set<F.Models.Entities.PdfUpload>().SingleAsync(); var claim = await db.Set<F.Models.Entities.PdfClaim>().SingleAsync();
            Assert.Equal(document, upload.DocumentId); Assert.Equal(document, claim.DocumentId); Assert.Equal(operation, claim.OperationId); Assert.Equal("Active", claim.State);
            Assert.Equal(upload.Sha256, Convert.ToHexString(SHA256.HashData(restoredBytes))); Assert.Equal(upload.SizeBytes, restoredBytes.Length);
        }
        var email = new CountingEmail();
        await using (var db = f.Notification(restored: true))
        {
            var replay = await new DurableNotifications(db).AcceptAsync(sender, key, message, default);
            Assert.Equal(receipt, replay.Id); Assert.Equal(5, await db.Set<DeliveryInbox>().CountAsync()); Assert.Equal(5, await db.InAppNotifications.CountAsync());
            foreach (var id in new[] { sending, unknown, sent, dead })
                Assert.Equal("NotClaimed", await new DurableDelivery(db, email, clock).ProcessAsync(id, default));
            Assert.Equal("UnknownOutcome", (await db.Set<DeliveryInbox>().AsNoTracking().SingleAsync(x => x.Id == sending)).State);
            Assert.Equal(0, email.Calls);
        }
        File.Copy(Path.Combine(restoredStorage, fileName), Path.Combine(f.Output, "bundle", "storage", fileName));
        await File.WriteAllTextAsync(Path.Combine(f.Output, "roundtrip.json"), JsonSerializer.Serialize(new {
            passed = true, synthetic = true, productionReady = false, cutId = f.CutId,
            profile = "core-five-stores", components = Fixture.Components, beforeDataSha256 = before, afterDataSha256 = after,
            pdfSha256 = hash.ToLowerInvariant(), pdfBytes = bytes.Length, backupCount = Fixture.Components.Length,
            noSmtpResendCalls = email.Calls, allWorkersStarted = false, elapsedMilliseconds = timer.ElapsedMilliseconds,
            checks = new[] { "real-backup-checksum-verifyonly-restore", "all-user-table-values-equal", "registration-replay-counter-unchanged", "edited-company-department-preserved", "rare-five-digit-preserved", "history-audit-current-pdf-claim", "task-and-reminder-correlations", "notification-replay-no-duplicates", "unknown-expired-sending-no-resend", "inactive-user-revoked-token-preserved", "directory-inbox-outbox-revision-preserved-no-authority-executed", "partner-contact-version-tombstone-audit-preserved" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class CountingEmail : IDeliveryEmail
    {
        public int Calls;
        public Task<string> SendAsync(DurableMessage message, Guid id, CancellationToken ct) { Calls++; return Task.FromResult("Sent"); }
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2027-01-04T02:00:00Z"); }

    private sealed class Fixture : IAsyncDisposable
    {
        public static readonly string[] Components = ["auth", "document", "files", "notification", "partner"];
        public readonly string CutId = Guid.NewGuid().ToString();
        private readonly string prefix = "das_restore_qa_" + Guid.NewGuid().ToString("N");
        private readonly List<string> owned = [];
        private readonly string master;
        public readonly string Output;
        public readonly string TempRoot;
        private Fixture()
        {
            if (Environment.GetEnvironmentVariable("DAS_RESTORE_DRILL") != "synthetic") throw new InvalidOperationException("Use the isolated restore QA runner.");
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION"));
            if (!connection.DataSource.StartsWith("127.0.0.1,", StringComparison.Ordinal) || connection.InitialCatalog != "master") throw new InvalidOperationException("Only isolated loopback master is allowed.");
            master = connection.ConnectionString;
            Output = Path.GetFullPath(Environment.GetEnvironmentVariable("DAS_RESTORE_OUTPUT")!);
            var repo = FindRoot(); var qa = Path.GetFullPath(Path.Combine(repo, ".artifacts", "qa")) + Path.DirectorySeparatorChar;
            if (!Output.StartsWith(qa, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.Combine(Output, "bundle", "storage"))) throw new InvalidOperationException("Output must be an existing owned QA directory.");
            for (var p = new DirectoryInfo(Output); p is not null; p = p.Parent)
                if ((p.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("No linked QA paths.");
            TempRoot = Path.Combine(Path.GetTempPath(), prefix); Directory.CreateDirectory(TempRoot);
        }
        private static string FindRoot()
        {
            for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
                if (File.Exists(Path.Combine(p.FullName, "tools", "qa", "run-isolated-sql.py")) ||
                    File.Exists(Path.Combine(p.FullName, "scripts", "qa", "run-isolated-sql.py"))) return p.FullName;
            throw new InvalidOperationException("Worktree not found.");
        }
        private string Name(string component, bool restored) => Components.Contains(component) ? prefix + "_" + component + (restored ? "_restored" : "_source") : throw new ArgumentException("Unknown component");
        private string Connection(string component, bool restored) => new SqlConnectionStringBuilder(master) { InitialCatalog = Name(component, restored) }.ConnectionString;
        public D.DocumentDbContext Document(bool restored = false) => new(new DbContextOptionsBuilder<D.DocumentDbContext>().UseSqlServer(Connection("document", restored)).Options);
        public F.Data.FileDbContext Files(bool restored = false) => new(new DbContextOptionsBuilder<F.Data.FileDbContext>().UseSqlServer(Connection("files", restored)).Options);
        public NotificationDbContext Notification(bool restored = false) => new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(Connection("notification", restored)).Options);
        public A.AuthDbContext Auth(bool restored = false) => new(new DbContextOptionsBuilder<A.AuthDbContext>().UseSqlServer(Connection("auth", restored)).Options);
        public P.PartnerDbContext Partner(bool restored = false) => new(new DbContextOptionsBuilder<P.PartnerDbContext>().UseSqlServer(Connection("partner", restored)).Options);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            try
            {
                foreach (var component in Components) { var name = f.Name(component, false); await f.Command("CREATE DATABASE [" + name + "]"); f.owned.Add(name); }
                await using (var db = f.Document()) await db.Database.MigrateAsync();
                await using (var db = f.Files()) await db.Database.MigrateAsync();
                await using (var db = f.Notification()) await db.Database.MigrateAsync();
                await using (var db = f.Auth()) await db.Database.MigrateAsync();
                await using (var db = f.Partner()) await db.Database.MigrateAsync();
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        private async Task Command(string text)
        {
            await using var connection = new SqlConnection(master); await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandTimeout = 120; command.CommandText = text; await command.ExecuteNonQueryAsync();
        }
        public async Task BackupAndRestore(string component)
        {
            var source = Name(component, false); var target = Name(component, true); var backup = "/var/opt/mssql/data/restore-qa/" + component + ".bak";
            await Command($"BACKUP DATABASE [{source}] TO DISK = '{backup}' WITH COPY_ONLY, CHECKSUM");
            await Command($"RESTORE VERIFYONLY FROM DISK = '{backup}' WITH CHECKSUM");
            // Logical names originate exclusively from the generated QA database (not an uploaded backup).
            var moves = new List<string>();
            await using (var connection = new SqlConnection(Connection(component, false)))
            {
                await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "SELECT name,type FROM sys.database_files ORDER BY file_id";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var logical = reader.GetString(0);
                    if (!logical.StartsWith(source, StringComparison.Ordinal) || logical.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))) throw new InvalidOperationException("Unexpected logical QA file name.");
                    var extension = reader.GetByte(1) == 1 ? ".ldf" : ".mdf";
                    moves.Add($"MOVE '{logical}' TO '/var/opt/mssql/data/{target}_{moves.Count}{extension}'");
                }
            }
            await using (var connection = new SqlConnection(master))
            {
                await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "SELECT DB_ID(@name)"; command.Parameters.AddWithValue("@name", target);
                Assert.Null((await command.ExecuteScalarAsync()) as int?);
            }
            await Command($"RESTORE DATABASE [{target}] FROM DISK = '{backup}' WITH CHECKSUM, {string.Join(", ", moves)}");
            owned.Add(target);
        }
        public async Task<string> DataHash(string component, bool restored)
        {
            await using var connection = new SqlConnection(Connection(component, restored)); await connection.OpenAsync();
            var tables = new List<(string Schema, string Table)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT s.name,t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name";
                await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
            }
            var all = new List<object>();
            foreach (var (schema, table) in tables)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM [" + schema.Replace("]", "]]") + "].[" + table.Replace("]", "]]") + "]";
                await using var reader = await command.ExecuteReaderAsync();
                var columns = Enumerable.Range(0, reader.FieldCount).Select(i => new { name = reader.GetName(i), type = reader.GetDataTypeName(i) }).ToArray();
                var rows = new List<string>();
                while (await reader.ReadAsync()) rows.Add(JsonSerializer.Serialize(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray()));
                rows.Sort(StringComparer.Ordinal); all.Add(new { schema, table, columns, rows });
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(all)))).ToLowerInvariant();
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            foreach (var name in owned.AsEnumerable().Reverse()) await Command($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
            var path = Path.GetFullPath(TempRoot); var boundary = Path.GetFullPath(Path.GetTempPath()) + (Path.EndsInDirectorySeparator(Path.GetTempPath()) ? "" : Path.DirectorySeparatorChar);
            if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path) != prefix) throw new InvalidOperationException("Unsafe fixture cleanup path.");
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }
}

public sealed class RestoreSqlFactAttribute : FactAttribute
{
    public RestoreSqlFactAttribute() { if (Environment.GetEnvironmentVariable("DAS_RESTORE_DRILL") != "synthetic") Skip = "Requires isolated synthetic restore runner."; }
}
