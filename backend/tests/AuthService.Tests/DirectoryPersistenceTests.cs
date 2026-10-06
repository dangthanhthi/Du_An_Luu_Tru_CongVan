using AuthService.Organization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AuthService.Tests;

public sealed class DirectoryPersistenceTests
{
    internal static readonly Guid Management = Guid.Parse("00000000-0000-0000-0000-000000000001");
    internal static readonly Guid Finance = Guid.Parse("00000000-0000-0000-0000-000000000002");
    internal static readonly Guid Group = Guid.Parse("00000000-0000-0000-0000-000000000003");
    internal static readonly Guid Gm = Guid.Parse("00000000-0000-0000-0000-000000000010");
    internal static readonly Guid Staff = Guid.Parse("00000000-0000-0000-0000-000000000011");
    internal static OrganizationDirectorySnapshot Snapshot(long sequence = 1) => new(sequence,
        [new(Management, "Management", "MGT", null, true, true), new(Finance, "Finance", "FIN", Management, true, true),
            new(Group, "Accounts", null, Finance, false, true)],
        [new(Gm, "GM", true), new(Staff, "Staff", true)], [new(Group, Staff, true)],
        [new(Management, Gm, []), new(Finance, Staff, [])]);

    [Fact]
    public async Task Applied_snapshot_survives_context_restart_with_inbox_and_outbox()
    {
        await using var fixture = await Fixture.Create();
        await using (var db = fixture.Db())
        {
            var result = await new OrganizationDirectoryStore(db, fixture.Clock).ApplyAsync("eap", "msg-1", Snapshot());
            Assert.Equal(DirectorySyncDisposition.Applied, result.Disposition);
            Assert.Equal(1, result.AuthorizationRevision);
        }
        await using var restarted = fixture.Db();
        var directory = await new OrganizationDirectoryStore(restarted, fixture.Clock).ReadFreshAsync("eap", TimeSpan.FromSeconds(60));
        Assert.Equal(new[] { Finance }, directory.Projection.GetActiveDepartmentIds(Staff));
        Assert.Equal(1, await restarted.Set<DirectoryInboxReceipt>().CountAsync());
        Assert.Equal(1, await restarted.Set<DirectoryOutboxEvent>().CountAsync());
    }

    [Fact]
    public async Task Same_message_replay_returns_recorded_result_without_revision_or_timestamp_change()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        var first = await store.ApplyAsync("eap", "msg-1", Snapshot());
        fixture.Clock.Now += TimeSpan.FromSeconds(30);
        var replay = await store.ApplyAsync("eap", "msg-1", Snapshot() with { Units = Snapshot().Units.Reverse().ToArray() });
        Assert.Equal(DirectorySyncDisposition.Applied, first.Disposition);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.AuthorizationRevision, replay.AuthorizationRevision);
        var head = await db.Set<DirectoryProjectionState>().AsNoTracking().SingleAsync();
        Assert.Equal(fixture.InitialTime, head.VerifiedAt);
        Assert.Equal(1, await db.Set<DirectoryOutboxEvent>().CountAsync());
    }

    [Fact]
    public async Task Reused_message_id_with_different_sequence_or_body_is_conflict()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "same", Snapshot());
        var conflict = await store.ApplyAsync("eap", "same", Snapshot(2));
        Assert.Equal(DirectorySyncDisposition.MessageConflict, conflict.Disposition);
        Assert.Equal(1, (await db.Set<DirectoryProjectionState>().SingleAsync()).Sequence);
    }

    [Fact]
    public async Task Older_snapshot_does_not_restore_revoked_membership_or_renew_freshness()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "newer", Snapshot(3) with { Memberships = [] });
        fixture.Clock.Now += TimeSpan.FromSeconds(20);
        var stale = await store.ApplyAsync("eap", "older", Snapshot(1));
        Assert.Equal(DirectorySyncDisposition.Unchanged, stale.Disposition);
        var loaded = await store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60));
        Assert.Empty(loaded.Projection.GetActiveDepartmentIds(Staff));
        Assert.Equal(fixture.InitialTime, loaded.VerifiedAt);
        Assert.Equal(1, loaded.AuthorizationRevision);
    }

    [Fact]
    public async Task Conflicting_sequence_and_invalid_hierarchy_never_replace_good_head()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "good", Snapshot());
        var conflict = await store.ApplyAsync("eap", "conflict", Snapshot() with { Memberships = [] });
        Assert.Equal(DirectorySyncDisposition.Rejected, conflict.Disposition);
        Assert.Contains(conflict.Errors, e => e.Code == "SNAPSHOT_SEQUENCE_CONFLICT");
        var invalid = await store.ApplyAsync("eap", "invalid", Snapshot(2) with
        { Units = Snapshot().Units.Select(x => x.Id == Group ? x with { ParentId = Guid.NewGuid() } : x).ToArray() });
        Assert.Equal(DirectorySyncDisposition.Rejected, invalid.Disposition);
        Assert.Contains(invalid.Errors, e => e.Code == "PARENT_NOT_FOUND");
        Assert.Equal(new[] { Finance }, (await store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60))).Projection.GetActiveDepartmentIds(Staff));
        Assert.Equal(1, await db.Set<DirectoryOutboxEvent>().CountAsync());
    }

    [Fact]
    public async Task Exception_after_database_save_rolls_back_head_inbox_and_outbox()
    {
        await using var fixture = await Fixture.Create();
        await using (var db = fixture.Db(new FailAfterSave()))
        {
            await Assert.ThrowsAsync<InjectedFailure>(() => new OrganizationDirectoryStore(db, fixture.Clock).ApplyAsync("eap", "crash", Snapshot()));
        }
        await using var verify = fixture.Db();
        Assert.Equal(0, await verify.Set<DirectoryProjectionState>().CountAsync());
        Assert.Equal(0, await verify.Set<DirectoryInboxReceipt>().CountAsync());
        Assert.Equal(0, await verify.Set<DirectoryOutboxEvent>().CountAsync());
    }

    [Fact]
    public async Task Missing_expired_or_future_verified_directory_fails_closed()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        Assert.Equal("DIRECTORY_NOT_READY", (await Assert.ThrowsAsync<DirectoryUnavailableException>(
            () => store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60)))).Code);
        await store.ApplyAsync("eap", "good", Snapshot());
        fixture.Clock.Now += TimeSpan.FromSeconds(61);
        Assert.Equal("DIRECTORY_STALE", (await Assert.ThrowsAsync<DirectoryUnavailableException>(
            () => store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60)))).Code);
        fixture.Clock.Now = fixture.InitialTime - TimeSpan.FromSeconds(1);
        Assert.Equal("DIRECTORY_CLOCK_INVALID", (await Assert.ThrowsAsync<DirectoryUnavailableException>(
            () => store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60)))).Code);
    }

    [Fact]
    public async Task Identical_snapshot_under_new_message_cannot_extend_freshness()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "first", Snapshot());
        fixture.Clock.Now += TimeSpan.FromSeconds(61);
        var replay = await store.ApplyAsync("eap", "new-envelope", Snapshot());
        Assert.Equal(DirectorySyncDisposition.Unchanged, replay.Disposition);
        await Assert.ThrowsAsync<DirectoryUnavailableException>(() => store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60)));
        Assert.Equal(1, await db.Set<DirectoryOutboxEvent>().CountAsync());
    }

    [Fact]
    public async Task Newer_snapshot_increments_revision_exactly_once_and_invalidates_old_membership()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Db();
        var store = new OrganizationDirectoryStore(db, fixture.Clock);
        await store.ApplyAsync("eap", "first", Snapshot());
        var applied = await store.ApplyAsync("eap", "second", Snapshot(2) with { Memberships = [] });
        Assert.Equal(2, applied.AuthorizationRevision);
        Assert.Empty((await store.ReadFreshAsync("eap", TimeSpan.FromSeconds(60))).Projection.GetActiveDepartmentIds(Staff));
        Assert.Equal(2, await db.Set<DirectoryOutboxEvent>().CountAsync());
    }

    internal sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public ManualClock Clock { get; } = new();
        public DateTimeOffset InitialTime => new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        public AuthDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite(connection).AddInterceptors(interceptors).Options);
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            await fixture.connection.OpenAsync();
            await using var db = fixture.Db();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
    private sealed class InjectedFailure : Exception;
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
            => throw new InjectedFailure();
    }
}

