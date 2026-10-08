using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DocumentService.Tests;

public sealed class J22AdminCatalogSqlTests
{
    [CatalogSqlFact]
    public async Task Retrying_SQL_read_holds_one_serializable_view_while_matching_writer_waits()
    {
        await using var fixture = await Fixture.Create();
        var probe = new ReadProbe(); await using var db = fixture.Db(probe);
        Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
        probe.StartWriter = async () =>
        {
            await using var writer = fixture.Db();
            writer.BusinessCatalogEntries.Add(new() { Group = "categories", Code = "LATE", Name = "Concurrent insert" });
            probe.WriterStarted.TrySetResult(); await writer.SaveChangesAsync();
        };
        var page = await new CatalogService(db, TimeProvider.System).GetAdminPageAsync(new("categories", "All", null, 1, 100), Guid.NewGuid());
        // A deadlock victim may retry the whole read after the writer commits; the returned count/page
        // must belong to that same successful transaction, including (or excluding) LATE together.
        Assert.InRange(page.TotalCount, 32, 33); Assert.Equal(page.TotalCount, page.Items.Count);
        Assert.Equal(page.TotalCount == 33, page.Items.Any(x => x.Code == "LATE"));
        Assert.True(probe.WriterWasBlockedDuringPage); Assert.True(probe.Reads.Count >= 2);
        Assert.All(probe.Reads, x => Assert.Equal(IsolationLevel.Serializable, x.Isolation)); Assert.Equal(probe.Reads[^2].TransactionId, probe.Reads[^1].TransactionId);
        await probe.Writer!.WaitAsync(TimeSpan.FromSeconds(15)); await using var verify = fixture.Db(); Assert.Equal(33, await verify.BusinessCatalogEntries.CountAsync(x => x.Group == "categories"));
    }

    [CatalogSqlFact]
    public async Task SQL_collation_literal_parameter_search_and_existing_index_plan_are_observed()
    {
        await using var fixture = await Fixture.Create(); var probe = new ReadProbe(); await using var db = fixture.Db(probe);
        db.BusinessCatalogEntries.AddRange(new BusinessCatalogEntry { Group = "categories", Code = "LITERAL", Name = "Pháp lý %_[] \\ ' OR 1=1 --" }, new BusinessCatalogEntry { Group = "categories", Code = "DECOY", Name = "Pháp lý ordinary" }); await db.SaveChangesAsync();
        var service = new CatalogService(db, TimeProvider.System);
        foreach (var term in new[] { "%_[]", "\\", "' OR 1=1 --", "PHÁP LÝ %_[]", "literal" })
        {
            var page = await service.GetAdminPageAsync(new("categories", "Active", term, 1, 20), Guid.NewGuid()); Assert.Equal(1, page.TotalCount); Assert.Equal("LITERAL", Assert.Single(page.Items).Code);
        }
        Assert.All(probe.SearchCommands, x => { Assert.NotEmpty(x.Parameters); Assert.DoesNotContain("OR 1=1", x.Text); });
        await service.GetAdminPageAsync(new("categories", "All", null, 1, 20), Guid.NewGuid());
        await fixture.WritePlanEvidence(probe.LastPage!);
    }

    [CatalogSqlFact]
    public async Task SQL_reactivation_writers_with_same_version_commit_one_identity_and_audit()
    {
        await using var fixture = await Fixture.Create(); Guid id;
        await using (var db = fixture.Db()) id = (await db.BusinessCatalogEntries.SingleAsync(x => x.Group == "categories" && x.Code == "C25")).Id;
        var statuses = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = fixture.Db(); try { await new CatalogService(db, TimeProvider.System).UpdateAsync(id, new("Fixture 25", 2, true, 1), Guid.NewGuid()); return 200; }
            catch (CatalogRuleException e) { return e.Status; }
        }));
        Assert.Equal(new[] { 200, 409 }, statuses.Order().ToArray()); await using var verify = fixture.Db();
        var row = await verify.BusinessCatalogEntries.SingleAsync(x => x.Id == id); Assert.True(row.IsActive); Assert.Equal("C25", row.Code); Assert.Equal("Fixture 25", row.Name); Assert.Equal(2, row.SortOrder); Assert.Equal(2, row.Version); Assert.Single(await verify.CatalogAuditEvents.Where(x => x.EntryId == id).ToListAsync());
    }

    private sealed record CapturedCommand(string Text, (string Name, object Value)[] Parameters);
    private sealed class ReadProbe : DbCommandInterceptor
    {
        internal readonly List<(Guid TransactionId, IsolationLevel Isolation)> Reads = [];
        internal readonly List<CapturedCommand> SearchCommands = [];
        internal CapturedCommand? LastPage;
        internal Func<Task>? StartWriter; internal Task? Writer;
        internal readonly TaskCompletionSource WriterStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool WriterWasBlockedDuringPage;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("BusinessCatalogEntries", StringComparison.Ordinal))
            {
                var transaction = eventData.Context!.Database.CurrentTransaction!;
                Reads.Add((transaction.TransactionId, transaction.GetDbTransaction().IsolationLevel));
                var captured = new CapturedCommand(command.CommandText, command.Parameters.Cast<DbParameter>().Select(x => (x.ParameterName, x.Value!)).ToArray());
                if (command.CommandText.Contains("LIKE", StringComparison.Ordinal)) SearchCommands.Add(captured);
                if (command.CommandText.Contains("ORDER BY", StringComparison.Ordinal))
                {
                    LastPage = captured;
                    if (Writer is not null) { await WriterStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct); await Task.Delay(150, ct); WriterWasBlockedDuringPage |= !Writer.IsCompleted; }
                }
            }
            return result;
        }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
        {
            if (StartWriter is not null && command.CommandText.Contains("COUNT(*)", StringComparison.Ordinal)) { var start = StartWriter; StartWriter = null; Writer = Task.Run(start); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string name = "das_j22_test_" + Guid.NewGuid().ToString("N");
        private readonly string master = Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION")!;
        private string Connection => new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString;
        internal DocumentDbContext Db(ReadProbe? probe = null)
        {
            var options = new DbContextOptionsBuilder<DocumentDbContext>().UseSqlServer(Connection, sql => sql.EnableRetryOnFailure(3));
            if (probe is not null) options.AddInterceptors(probe); return new(options.Options);
        }
        internal static async Task<Fixture> Create()
        {
            var fixture = new Fixture(); try
            {
                await using var connection = new SqlConnection(fixture.master); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "CREATE DATABASE [" + fixture.name + "]"; await command.ExecuteNonQueryAsync();
                await using var db = fixture.Db(); await db.Database.MigrateAsync(); db.BusinessCatalogEntries.AddRange(Enumerable.Range(0, 32).Select(i => new BusinessCatalogEntry { Group = "categories", Code = $"C{i:00}", Name = $"Fixture {i}", SortOrder = i / 10, IsActive = i < 25 })); await db.SaveChangesAsync(); return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        internal async Task WritePlanEvidence(CapturedCommand query)
        {
            var evidenceRoot = Environment.GetEnvironmentVariable("DAS_J22_EVIDENCE_DIR"); if (string.IsNullOrWhiteSpace(evidenceRoot)) return;
            await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
            await using var metadata = connection.CreateCommand(); metadata.CommandText = "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')); SELECT i.name, i.is_unique, c.name AS column_name, ic.key_ordinal FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('document.BusinessCatalogEntries') ORDER BY i.name, ic.key_ordinal;";
            var lines = new List<string> { "Synthetic isolated SQL fixture. Actual SQL plan, not customer workload performance acceptance." };
            await using (var reader = await metadata.ExecuteReaderAsync()) { while (await reader.ReadAsync()) lines.Add("Collation: " + reader.GetString(0)); await reader.NextResultAsync(); while (await reader.ReadAsync()) lines.Add($"Index: {reader.GetString(0)}, unique={reader.GetBoolean(1)}, column={reader.GetString(2)}, ordinal={reader.GetByte(3)}"); }
            Assert.Contains(lines, x => x.StartsWith("Index: IX_BusinessCatalogEntries_Group_Code", StringComparison.Ordinal));
            await using var switchCommand = connection.CreateCommand(); switchCommand.CommandText = "SET STATISTICS XML ON"; await switchCommand.ExecuteNonQueryAsync();
            try
            {
                await using var plan = connection.CreateCommand(); plan.CommandText = query.Text; foreach (var parameter in query.Parameters) plan.Parameters.AddWithValue(parameter.Name, parameter.Value);
                string? xml = null;
                await using (var reader = await plan.ExecuteReaderAsync())
                {
                    do { while (await reader.ReadAsync()) if (reader.FieldCount == 1) { var value = reader.GetString(0); if (value.Contains("ShowPlanXML", StringComparison.Ordinal)) xml = value; } } while (await reader.NextResultAsync());
                }
                Assert.False(string.IsNullOrWhiteSpace(xml)); await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "task-3-sql-plan.xml"), xml);
                lines.Add(query.Text); lines.Add("All user filters captured as parameters. Existing indexes measured; no migration/index added."); await File.WriteAllLinesAsync(Path.Combine(evidenceRoot, "task-3-sql-index-plan.txt"), lines);
            }
            finally { switchCommand.CommandText = "SET STATISTICS XML OFF"; await switchCommand.ExecuteNonQueryAsync(); }
        }
        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools(); await using var connection = new SqlConnection(master); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "IF DB_ID('" + name + "') IS NOT NULL BEGIN ALTER DATABASE [" + name + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + name + "]; END"; await command.ExecuteNonQueryAsync();
        }
    }
}
