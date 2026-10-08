using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmailWorkerService.Data;
using EmailWorkerService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmailWorkerService.Tests;

public sealed class EmailRestoreSqlTests
{
    [EmailRestoreSqlFact]
    public async Task Real_backup_restores_settings_scan_correlations_nullable_progress_and_migration_history_with_workers_off()
    {
        await using var f = await Fixture.Create();
        var at = new DateTime(2027, 1, 4, 1, 0, 0, DateTimeKind.Utc);
        var scan = new EmailScanLog { StartedAt = at, FinishedAt = null, EmailsScanned = 4, TotalEmails = 5,
            DocumentsCreated = 1, ReadyForIntakeCount = 1, FailedCount = 1, Success = false,
            CurrentEmailSubject = "Synthetic paused scan", CurrentSenderEmail = "sender@example.invalid", TriggerType = "Manual" };
        var settings = new EmailImapSettings { ImapHost = "mail.example.invalid", EmailAddress = "qa@example.invalid",
            AppPassword = "!synthetic-unusable-password", WhitelistedDomains = "example.invalid", AutoScanIntervalMinutes = 37, UpdatedAt = at };
        var states = new[] { "ReadyForIntake", "IntakeCompleted", "UploadFailed", "OcrFailed", "Scanning" };
        var items = states.Select((state, index) => new EmailScanItemLog { ScanLogId = scan.Id, ReceivedAt = at.AddMinutes(index),
            SenderEmail = "sender@example.invalid", Subject = "Synthetic " + index, AttachmentName = index == 4 ? null : "synthetic.pdf",
            FileId = index == 4 ? null : Guid.NewGuid(), PartnerId = index == 4 ? null : Guid.NewGuid(),
            ExtractedReferenceNumber = index == 4 ? null : "SYNTHETIC-" + index, ExtractedSubject = index == 4 ? null : "Synthetic extracted subject",
            DocumentId = index == 1 ? Guid.NewGuid().ToString() : null, Status = state,
            ErrorMessage = index == 2 ? "Synthetic upload failure" : index == 3 ? "Synthetic OCR failure" : null,
            ProcessedAt = index == 4 ? null : at.AddMinutes(index + 1), IntakeConfirmedAt = index == 1 ? at.AddMinutes(2) : null }).ToArray();
        await using (var db = f.Db())
        {
            db.EmailImapSettings.Add(settings); db.EmailScanLogs.Add(scan); db.EmailScanItemLogs.AddRange(items);
            await db.SaveChangesAsync();
        }
        // No hosts or scanner/SMTP clients are constructed; writes stop before this independent backup cut.
        var before = await f.DataHash(false);
        await f.BackupAndRestore();
        var after = await f.DataHash(true);
        Assert.Equal(before, after);
        int migrationCount;
        await using (var db = f.Db(true))
        {
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray(); Assert.NotEmpty(migrations); migrationCount = migrations.Length;
            var saved = await db.EmailImapSettings.AsNoTracking().SingleAsync();
            Assert.Equal(settings.Id, saved.Id); Assert.Equal("mail.example.invalid", saved.ImapHost);
            Assert.Equal("!synthetic-unusable-password", saved.AppPassword); Assert.Equal(37, saved.AutoScanIntervalMinutes); Assert.Equal(at, saved.UpdatedAt);
            var log = await db.EmailScanLogs.AsNoTracking().SingleAsync(); Assert.Equal(scan.Id, log.Id); Assert.Null(log.FinishedAt); Assert.False(log.Success);
            var restored = await db.EmailScanItemLogs.AsNoTracking().ToDictionaryAsync(x => x.Id);
            Assert.Equal(5, restored.Count);
            foreach (var item in items)
            {
                var actual = restored[item.Id]; Assert.Equal(scan.Id, actual.ScanLogId); Assert.Equal(item.Status, actual.Status);
                Assert.Equal(item.FileId, actual.FileId); Assert.Equal(item.PartnerId, actual.PartnerId); Assert.Equal(item.DocumentId, actual.DocumentId);
                Assert.Equal(item.ReceivedAt, actual.ReceivedAt); Assert.Equal(item.ProcessedAt, actual.ProcessedAt);
                Assert.Equal(item.IntakeConfirmedAt, actual.IntakeConfirmedAt); Assert.Equal(item.ErrorMessage, actual.ErrorMessage);
            }
            // Restored FK rejects an orphan and still cascades; these mutations occur only after hash equality.
            db.EmailImapSettings.Add(new EmailImapSettings { Id = 2 });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            db.EmailScanItemLogs.Add(new EmailScanItemLog { ScanLogId = Guid.NewGuid(), SenderEmail = "orphan@example.invalid" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            db.EmailScanLogs.Remove(await db.EmailScanLogs.SingleAsync()); await db.SaveChangesAsync();
            Assert.Empty(await db.EmailScanItemLogs.ToListAsync()); Assert.Single(await db.EmailImapSettings.ToListAsync());
        }
        var report = new { passed = true, synthetic = true, productionReady = false, profile = "email-worker-store",
            cutId = f.CutId, components = new[] { "emailworker" }, backupCount = 1, allWorkersStarted = false,
            workerFlags = f.WorkerFlags, beforeDataSha256 = new Dictionary<string,string> { ["emailworker"] = before },
            afterDataSha256 = new Dictionary<string,string> { ["emailworker"] = after },
            rowCounts = new { settings = 1, scanLogs = 1, scanItems = 5, migrations = migrationCount } };
        // Report contains only metadata and hashes, never mailbox settings or raw rows.
        await using var output = new FileStream(Path.Combine(f.Output, "roundtrip.json"), FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions { WriteIndented = true });
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string master;
        private readonly string source = "das_email_restore_qa_" + Guid.NewGuid().ToString("N");
        private readonly List<string> owned = [];
        private string Target => source + "_restored";
        public string CutId { get; } = Guid.NewGuid().ToString();
        public string Output { get; }
        public Dictionary<string, bool> WorkerFlags { get; } = [];
        private Fixture()
        {
            if (Environment.GetEnvironmentVariable("DAS_RESTORE_DRILL") != "synthetic") throw new InvalidOperationException("Owned synthetic restore runner required.");
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION"));
            if (!Regex.IsMatch(connection.DataSource, @"^127\.0\.0\.1,[1-9][0-9]{0,4}$") || connection.InitialCatalog != "master")
                throw new InvalidOperationException("Owned loopback master required.");
            master = connection.ConnectionString;
            foreach (var key in new[] { "EmailIntake__WorkerEnabled", "EmailIntake__ManualScanEnabled", "Delivery__WorkerEnabled", "Reminders__Enabled" })
            {
                if (Environment.GetEnvironmentVariable(key) != "false") throw new InvalidOperationException("All workers must be explicitly disabled.");
                WorkerFlags[key] = false;
            }
            Output = Path.GetFullPath(Environment.GetEnvironmentVariable("DAS_RESTORE_OUTPUT")!);
            DirectoryInfo? root = new(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools", "qa", "run-isolated-sql.py")) &&
                !File.Exists(Path.Combine(root.FullName, "scripts", "qa", "run-isolated-sql.py"))) root = root.Parent;
            if (root is null || !Output.StartsWith(Path.Combine(root.FullName, ".artifacts", "qa") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(Output) != "restore-email" || !Directory.Exists(Output)) throw new InvalidOperationException("Existing owned QA output required.");
            for (var p = new DirectoryInfo(Output); p is not null; p = p.Parent)
                if ((p.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("No linked QA paths.");
        }
        private string Connection(bool restored) => new SqlConnectionStringBuilder(master) { InitialCatalog = restored ? Target : source }.ConnectionString;
        public EmailWorkerDbContext Db(bool restored = false) => new(new DbContextOptionsBuilder<EmailWorkerDbContext>().UseSqlServer(Connection(restored)).Options);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            try
            {
                f.owned.Add(f.source); await f.Command($"CREATE DATABASE [{f.source}]");
                await using var db = f.Db(); await db.Database.MigrateAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        private async Task Command(string text)
        {
            await using var connection = new SqlConnection(master); await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandTimeout = 120; command.CommandText = text; await command.ExecuteNonQueryAsync();
        }
        public async Task BackupAndRestore()
        {
            const string backup = "/var/opt/mssql/data/restore-qa/emailworker.bak";
            await Command($"BACKUP DATABASE [{source}] TO DISK = '{backup}' WITH COPY_ONLY, CHECKSUM");
            await Command($"RESTORE VERIFYONLY FROM DISK = '{backup}' WITH CHECKSUM");
            var moves = new List<string>();
            await using (var connection = new SqlConnection(master))
            {
                await connection.OpenAsync();
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DB_ID(@name)"; command.Parameters.AddWithValue("@name", Target);
                    Assert.IsType<DBNull>(await command.ExecuteScalarAsync());
                }
                await using var files = connection.CreateCommand(); files.CommandTimeout = 120;
                files.CommandText = $"RESTORE FILELISTONLY FROM DISK = '{backup}'";
                await using var reader = await files.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var logical = reader.GetString(reader.GetOrdinal("LogicalName")); var type = reader.GetString(reader.GetOrdinal("Type"));
                    if (!logical.StartsWith(source, StringComparison.Ordinal) || logical.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')) || type is not ("D" or "L"))
                        throw new InvalidOperationException("Unexpected logical file in owned backup.");
                    moves.Add($"MOVE '{logical}' TO '/var/opt/mssql/data/{Target}_{moves.Count}{(type == "L" ? ".ldf" : ".mdf")}'");
                }
            }
            Assert.Equal(2, moves.Count);
            owned.Add(Target); // Track intent so a timed-out restore is also cleaned up.
            await Command($"RESTORE DATABASE [{Target}] FROM DISK = '{backup}' WITH CHECKSUM, {string.Join(", ", moves)}");
        }
        public async Task<string> DataHash(bool restored)
        {
            await using var connection = new SqlConnection(Connection(restored)); await connection.OpenAsync();
            var tables = new List<(string Schema, string Table)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT s.name,t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name";
                await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) tables.Add((reader.GetString(0),reader.GetString(1)));
            }
            Assert.Equal(new[] { "dbo.__EFMigrationsHistory", "emailworker.EmailImapSettings", "emailworker.EmailScanItemLogs", "emailworker.EmailScanLogs" },
                tables.Select(x => x.Schema + "." + x.Table).ToArray());
            var all = new List<object>();
            foreach (var (schema, table) in tables)
            {
                await using var command = connection.CreateCommand(); command.CommandText = $"SELECT * FROM [{schema}].[{table}]";
                await using var reader = await command.ExecuteReaderAsync();
                var columns = Enumerable.Range(0,reader.FieldCount).Select(i => new { name = reader.GetName(i), type = reader.GetDataTypeName(i) }).ToArray();
                var rows = new List<string>();
                while (await reader.ReadAsync()) rows.Add(JsonSerializer.Serialize(Enumerable.Range(0,reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray()));
                rows.Sort(StringComparer.Ordinal); all.Add(new { schema, table, columns, rows });
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(all)))).ToLowerInvariant();
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            foreach (var name in owned.AsEnumerable().Reverse())
                await Command($"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END");
        }
    }
}

public sealed class EmailRestoreSqlFactAttribute : FactAttribute
{
    public EmailRestoreSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DAS_RESTORE_DRILL") != "synthetic" || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")))
            Skip = "Requires the owned synthetic SQL restore runner.";
    }
}
