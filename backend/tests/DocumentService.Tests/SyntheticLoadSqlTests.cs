using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

[Collection("Synthetic SQL load isolation")]
public sealed class SyntheticLoadSqlTests
{
    [SyntheticLoadFact]
    public async Task Bounded_SQL_load_and_exact_replays_preserve_three_counters_and_atomic_graphs()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DAS_TEST_SQL_CONNECTION"));
        Assert.StartsWith("127.0.0.1,", connection.DataSource); Assert.Equal("master", connection.InitialCatalog);
        var output = Path.GetFullPath(Environment.GetEnvironmentVariable("DAS_LOAD_OUTPUT")!);
        var root = FindRoot(); var boundary = Path.GetFullPath(Path.Combine(root, ".artifacts", "qa")) + Path.DirectorySeparatorChar;
        Assert.StartsWith(boundary, output, StringComparison.OrdinalIgnoreCase);
        for (var p = new DirectoryInfo(output); p is not null; p = p.Parent)
            Assert.Equal(0, (int)(p.Attributes & FileAttributes.ReparsePoint));
        Assert.True(Directory.Exists(output)); Assert.False(File.Exists(Path.Combine(output, "load.json")));
        await using var fixture = await RegistrationSqlTests.Fixture.Create();
        const int unique = 300; const int concurrency = 8;
        var kinds = new[] { "INCOMING", "OUTGOING", "INTERNAL" };
        var clock = new V2PersistenceTests.Clock("2027-01-04T02:00:00Z");
        var timings = new ConcurrentBag<double>(); var replays = new ConcurrentBag<double>();
        var documents = new ConcurrentBag<(Guid Id, string Number, string Kind)>();
        var timer = Stopwatch.StartNew();
        await Parallel.ForEachAsync(Enumerable.Range(0, unique), new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async (i, ct) =>
        {
            var kind = kinds[i % 3];
            var identity = V2PersistenceTests.Identity with { OwnerDepartmentCode = i % 2 == 0 ? "ADM" : "FIN", OwnerDepartmentId = i % 2 == 0 ? V2PersistenceTests.Identity.OwnerDepartmentId : OtherDepartment };
            var draft = V2PersistenceTests.Draft(kind, "Synthetic load " + i) with { CompanyCode = i % 2 == 0 ? "HL" : "HV", OwnerDepartmentId = identity.OwnerDepartmentId };
            var key = "synthetic-load-" + i;
            await using var db = fixture.Db(); var service = new V2RegistrationService(db, clock);
            var watch = Stopwatch.StartNew(); var document = await service.RegisterAsync(draft, identity, key, ct); timings.Add(watch.Elapsed.TotalMilliseconds);
            documents.Add((document.Id, document.DocumentNumber, kind));
            watch.Restart(); var replay = await service.RegisterAsync(draft, identity, key, ct); replays.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(document.Id, replay.Id); Assert.Equal(document.DocumentNumber, replay.DocumentNumber);
        });
        timer.Stop();
        Assert.Equal(unique, documents.Select(x => x.Id).Distinct().Count());
        Assert.Equal(unique, documents.Select(x => x.Number).Distinct().Count());
        await using var verify = fixture.Db();
        var counters = await verify.DocumentNumberCounters.AsNoTracking().OrderBy(x => x.DocType).ToListAsync();
        Assert.Equal(3, counters.Count); Assert.All(counters, x => Assert.Equal(100, x.CurrentValue));
        Assert.Equal(unique, await verify.Documents.CountAsync()); Assert.Equal(unique, await verify.DocumentRegistrations.CountAsync());
        Assert.Equal(unique, await verify.RegistrationRequests.CountAsync()); Assert.Equal(unique, await verify.DocumentStatusHistory.CountAsync());
        Assert.Equal(unique, await verify.DocumentOutboxEvents.CountAsync());
        foreach (var kind in kinds)
        {
            var sequences = await verify.DocumentRegistrations.Where(x => x.Kind == kind).OrderBy(x => x.SequenceNumber).Select(x => x.SequenceNumber).ToListAsync();
            Assert.Equal(Enumerable.Range(1, 100), sequences);
        }
        // A conflicting body on an existing key must reject without consuming a number or mutating any graph.
        var conflict = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => new V2RegistrationService(verify, clock)
            .RegisterAsync(V2PersistenceTests.Draft("INCOMING", "Conflicting body"), V2PersistenceTests.Identity, "synthetic-load-0"));
        Assert.Equal(409, conflict.Status);
        Assert.Equal(300, (await verify.DocumentNumberCounters.AsNoTracking().ToListAsync()).Sum(x => x.CurrentValue));
        Assert.Equal(unique, await verify.RegistrationRequests.CountAsync()); Assert.Equal(unique, await verify.DocumentOutboxEvents.CountAsync());
        static object Stats(IEnumerable<double> values)
        {
            var sorted = values.Order().ToArray();
            return new { count = sorted.Length, minimumMs = sorted[0], p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], maximumMs = sorted[^1] };
        }
        await File.WriteAllTextAsync(Path.Combine(output, "load.json"), JsonSerializer.Serialize(new {
            passed = true, synthetic = true, productionReady = false, scenario = "direct-service-SQL-no-HTTP", uniqueRegistrations = unique,
            exactReplays = replays.Count, concurrency, errors = 0, conflictsRejected = 1, registration = Stats(timings), replay = Stats(replays),
            elapsedMilliseconds = timer.ElapsedMilliseconds, serviceCallsPerSecond = 600 / timer.Elapsed.TotalSeconds,
            workersStarted = false, externalCalls = 0, slaAccepted = false,
            counters = counters.Select(x => new { x.DocType, x.CurrentValue }),
            atomicGraphCounts = new { documents = unique, headers = unique, receipts = unique, statusHistory = unique, outbox = unique },
            checks = new[] { "three-shared-counters", "300unique-300replay", "no-gap-no-duplicate", "atomic-graphs", "409-conflict-counter-unchanged" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static readonly Guid OtherDepartment = Guid.NewGuid();
    private static string FindRoot()
    {
        for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
            if (File.Exists(Path.Combine(p.FullName, "scripts", "qa", "run-synthetic-load.ps1"))) return p.FullName;
        throw new InvalidOperationException("Owned QA worktree not found.");
    }
}

public sealed class SyntheticLoadFactAttribute : FactAttribute
{
    public SyntheticLoadFactAttribute() { if (Environment.GetEnvironmentVariable("DAS_SYNTHETIC_LOAD") != "enabled") Skip = "Use the isolated synthetic load runner."; }
}

[CollectionDefinition("Synthetic SQL load isolation", DisableParallelization = true)]
public sealed class SyntheticLoadCollection;
