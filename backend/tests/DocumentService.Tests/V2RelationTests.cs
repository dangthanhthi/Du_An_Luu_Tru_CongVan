using System.Text.Json;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2RelationTests
{
    [Fact]
    public async Task Empty_registration_selection_preserves_the_previous_header_only_receipt_intent()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var first = await Register(f.Db, "INTERNAL", key: "empty-selection");
        var replay = await Register(f.Db, "INTERNAL", [], key: "empty-selection");
        Assert.Equal(first.Id, replay.Id); Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync());
        Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Changing_registration_relation_intent_with_the_same_key_conflicts_without_touching_either_target()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var a = await Register(f.Db, "OUTGOING"); var b = await Register(f.Db, "OUTGOING");
        await Register(f.Db, "INCOMING", [a.Id], Scope(a.Id), "relation-intent");
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Register(f.Db, "INCOMING", [b.Id], Scope(b.Id), "relation-intent"));
        Assert.Equal(409, error.Status); Assert.Equal(2, (await f.Db.DocumentRegistrations.SingleAsync(x => x.DocumentId == a.Id)).Version);
        Assert.Equal(1, (await f.Db.DocumentRegistrations.SingleAsync(x => x.DocumentId == b.Id)).Version);
        Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync()); Assert.Equal(3, await f.Db.Documents.CountAsync());
    }

    [Theory]
    [InlineData("source")][InlineData("counterpart")]
    public async Task Version_exhaustion_on_either_endpoint_rejects_the_entire_mutation(string endpoint)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, "INCOMING"); var target = await Register(f.Db, "OUTGOING");
        var id = endpoint == "source" ? source.Id : target.Id; var header = await f.Db.DocumentRegistrations.SingleAsync(x => x.DocumentId == id);
        header.Version = long.MaxValue; await f.Db.SaveChangesAsync();
        var draft = Edit(source, new([target.Id])) with { ExpectedVersion = endpoint == "source" ? long.MaxValue : 1 };
        var e = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(source.Id,
            draft, V2EditingTests.Actor(), relationScope: Scope(source.Id, target.Id)));
        Assert.Equal(409, e.Status); Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync()); Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(2, await f.Db.DocumentOutboxEvents.CountAsync());
    }

    [Fact]
    public async Task Query_hides_a_deleted_counterpart_and_rejects_a_scope_bound_to_another_actor()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, "INCOMING"); var target = await Register(f.Db, "OUTGOING");
        var scope = Scope(source.Id, target.Id); await Update(f.Db, source, new([target.Id]), scope);
        var stored = await f.Db.Documents.SingleAsync(x => x.Id == target.Id); stored.IsDeleted = true; await f.Db.SaveChangesAsync();
        Assert.Empty(await new V2DocumentRelations(f.Db).GetRelatedIdsAsync(source.Id, V2EditingTests.Actor(), scope));
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => new V2DocumentRelations(f.Db).GetRelatedIdsAsync(source.Id,
            V2EditingTests.Actor(), scope with { ActorUserId = Guid.NewGuid() })); Assert.Equal(403, error.Status);
        Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
    }

    [Fact]
    public async Task SQLite_relation_upgrade_preserves_headers_and_repeats_without_backfilling_edges()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, "INCOMING"); var target = await Register(f.Db, "OUTGOING");
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE DocumentRelations;");
        await SqliteG1Upgrade.ApplyAsync(f.Db); await SqliteG1Upgrade.ApplyAsync(f.Db); f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync()); Assert.All(await f.Db.DocumentRegistrations.ToListAsync(), x => Assert.Equal(1, x.Version));
        await Update(f.Db, source, new([target.Id]), Scope(source.Id, target.Id)); Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
    }

    [Theory]
    [InlineData("edit")][InlineData("register")]
    public async Task Lost_acknowledgement_after_actual_relation_COMMIT_does_not_repeat_either_endpoint_touch(string operation)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var fault = new LostCommit();
        var options = new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection, sql => sql.ExecutionStrategy(d => new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db = new DocumentDbContext(options); await db.Database.EnsureCreatedAsync(); var target = await Register(db, "OUTGOING");
        if (operation == "edit")
        {
            var source = await Register(db, "INCOMING"); fault.Enabled = true; await Update(db, source, new([target.Id]), Scope(source.Id, target.Id));
            Assert.Equal(2, await db.Set<DocumentEditAudit>().CountAsync()); Assert.Equal(4, await db.DocumentOutboxEvents.CountAsync());
        }
        else
        {
            fault.Enabled = true; await Register(db, "INCOMING", [target.Id], Scope(target.Id));
            Assert.Single(await db.Set<DocumentEditAudit>().ToListAsync()); Assert.Equal(3, await db.DocumentOutboxEvents.CountAsync());
        }
        Assert.Equal(1, fault.Failures); Assert.Single(await db.Set<DocumentRelation>().ToListAsync());
        Assert.Equal(2, (await db.DocumentRegistrations.SingleAsync(x => x.DocumentId == target.Id)).Version);
        Assert.Equal(2, (await db.DocumentNumberCounters.ToListAsync()).Sum(x => x.CurrentValue));
    }

    [Fact]
    public async Task Registration_failure_after_SaveChanges_rolls_back_edge_counter_receipt_and_counterpart_version()
    {
        var fault = new SavedFailure(); await using var f = await V2EditingTests.Fixture.Create(fault); var target = await Register(f.Db, "OUTGOING"); fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => Register(f.Db, "INCOMING", [target.Id], Scope(target.Id)));
        Assert.False(f.Db.ChangeTracker.HasChanges()); Assert.Single(await f.Db.Documents.ToListAsync()); Assert.Single(await f.Db.RegistrationRequests.ToListAsync());
        Assert.Single(await f.Db.DocumentOutboxEvents.ToListAsync()); Assert.Equal(1, (await f.Db.DocumentRegistrations.SingleAsync()).Version);
        Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync()); Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    private sealed class RetryOnce(ExecutionStrategyDependencies d) : ExecutionStrategy(d, 1, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception error) => error is TimeoutException; }
    private sealed class LostCommit : DbTransactionInterceptor
    {
        public bool Enabled; public int Failures;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        { if (Enabled) { Enabled = false; Failures++; throw new TimeoutException("Injected relation acknowledgement loss after COMMIT"); } return Task.CompletedTask; }
    }
    private sealed class SavedFailure : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        { if (Enabled) throw new IOException("Injected failure after relation graph SaveChanges"); return ValueTask.FromResult(result); }
    }
    [Fact]
    public async Task Database_relation_cannot_reference_a_document_without_a_v2_header()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, "INCOMING");
        var legacy = new Document { DocType = "OUTGOING", Title = "Legacy", DocumentNumber = "Old outgoing", CreatedByUserId = Guid.NewGuid() };
        f.Db.Documents.Add(legacy); await f.Db.SaveChangesAsync();
        f.Db.Set<DocumentRelation>().Add(new() { IncomingDocumentId = source.Id, OutgoingDocumentId = legacy.Id,
            CreatedByUserId = V2EditingTests.Actor().UserId, CreatedAt = V2EditingTests.Clock.GetUtcNow() });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync());
    }
    internal static Task<Document> Register(DocumentDbContext db, string kind, IReadOnlyList<Guid>? related = null,
        V2RelationScope? scope = null, string? key = null, RegistrationIdentity? identity = null) =>
        new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(
            V2PersistenceTests.Draft(kind) with { RelatedDocumentIds = related, OriginatorUserId = (identity ?? V2PersistenceTests.Identity).OriginatorUserId,
                OwnerDepartmentId = (identity ?? V2PersistenceTests.Identity).OwnerDepartmentId }, identity ?? V2PersistenceTests.Identity,
            key ?? Guid.NewGuid().ToString("N"), relationScope: scope);
    internal static V2RelationScope Scope(params Guid[] ids) => new(V2EditingTests.Actor().UserId, ids.ToHashSet());
    internal static V2EditDraft Edit(Document source, V2RelationChange? change) => V2KindDetailsTests.Header(source, null) with { Relations = change };
    private static Task<Document> Update(DocumentDbContext db, Document source, V2RelationChange change, V2RelationScope? scope = null,
        V2EditorActor? actor = null) => new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(source.Id, Edit(source, change), actor ?? V2EditingTests.Actor(), relationScope: scope);

    [Fact]
    public async Task Linking_from_incoming_persists_one_edge_and_advances_both_endpoints_once()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var incoming = await Register(f.Db, "INCOMING"); var outgoing = await Register(f.Db, "OUTGOING");
        await Update(f.Db, incoming, new([outgoing.Id]), Scope(incoming.Id, outgoing.Id));
        f.Db.ChangeTracker.Clear(); var edge = await f.Db.Set<DocumentRelation>().SingleAsync();
        Assert.Equal(incoming.Id, edge.IncomingDocumentId); Assert.Equal(outgoing.Id, edge.OutgoingDocumentId);
        Assert.Equal(V2EditingTests.Actor().UserId, edge.CreatedByUserId); Assert.Equal(V2EditingTests.Clock.GetUtcNow(), edge.CreatedAt);
        Assert.All(await f.Db.DocumentRegistrations.ToListAsync(), h => { Assert.Equal(2, h.Version); Assert.Equal(V2EditingTests.Actor().UserId, h.LastModifierUserId); });
        Assert.Equal(2, await f.Db.Set<DocumentEditAudit>().CountAsync()); Assert.Equal(4, await f.Db.DocumentOutboxEvents.CountAsync());
        Assert.Equal(2, (await f.Db.DocumentNumberCounters.ToListAsync()).Sum(x => x.CurrentValue)); Assert.Empty(await f.Db.DocumentDepartmentAccess.ToListAsync());
        var audit = await f.Db.Set<DocumentEditAudit>().SingleAsync(x => x.DocumentId == outgoing.Id);
        using var changes = JsonDocument.Parse(audit.ChangesJson);
        Assert.Equal(incoming.Id, changes.RootElement.GetProperty("RelatedDocumentIds").GetProperty("after")[0].GetGuid());
    }

    [Fact]
    public async Task One_canonical_edge_is_visible_from_both_ends_only_with_both_read_permissions()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var incoming = await Register(f.Db, "INCOMING"); var outgoing = await Register(f.Db, "OUTGOING");
        await Update(f.Db, outgoing, new([incoming.Id]), Scope(incoming.Id, outgoing.Id));
        var service = new V2DocumentRelations(f.Db); var actor = V2EditingTests.Actor();
        Assert.Equal(outgoing.Id, Assert.Single(await service.GetRelatedIdsAsync(incoming.Id, actor, Scope(incoming.Id, outgoing.Id))));
        Assert.Equal(incoming.Id, Assert.Single(await service.GetRelatedIdsAsync(outgoing.Id, actor, Scope(incoming.Id, outgoing.Id))));
        Assert.Empty(await service.GetRelatedIdsAsync(incoming.Id, actor, Scope(incoming.Id)));
        var missing = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => service.GetRelatedIdsAsync(incoming.Id, actor, Scope(outgoing.Id)));
        Assert.Equal(404, missing.Status); Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
    }

    [Fact]
    public async Task Metadata_and_relations_share_source_version_audit_and_transaction()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var incoming = await Register(f.Db, "INCOMING"); var outgoing = await Register(f.Db, "OUTGOING");
        var draft = Edit(outgoing, new([incoming.Id])) with { Subject = "Edited and linked", CompanyCode = "HV", Details = V2KindDetailsTests.Outgoing() };
        var result = await new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(outgoing.Id, draft, V2EditingTests.Actor(),
            references: V2KindDetailsTests.References(), relationScope: Scope(incoming.Id, outgoing.Id));
        Assert.Equal("27-01-0001/HV/ADM", result.DocumentNumber); Assert.Equal(2, result.Registration!.Version);
        Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync()); Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
        var audit = await f.Db.Set<DocumentEditAudit>().SingleAsync(x => x.DocumentId == outgoing.Id);
        using var diff = JsonDocument.Parse(audit.ChangesJson); Assert.Equal("Edited and linked", diff.RootElement.GetProperty("Subject").GetProperty("after").GetString());
        Assert.True(diff.RootElement.TryGetProperty("KindDetails", out _)); Assert.Equal(incoming.Id, diff.RootElement.GetProperty("RelatedDocumentIds").GetProperty("after")[0].GetGuid());
        Assert.Equal(4, await f.Db.DocumentOutboxEvents.CountAsync());
    }

    [Fact]
    public async Task Delta_removal_preserves_another_hidden_edge_and_noop_does_not_advance_versions()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var incoming = await Register(f.Db, "INCOMING");
        var outgoing1 = await Register(f.Db, "OUTGOING"); var outgoing2 = await Register(f.Db, "OUTGOING");
        var linked = await Update(f.Db, incoming, new([outgoing1.Id, outgoing2.Id]), Scope(incoming.Id, outgoing1.Id, outgoing2.Id));
        var unlinked = await Update(f.Db, linked, new(RemovedIds: [outgoing1.Id]), Scope(incoming.Id, outgoing1.Id));
        Assert.Equal(3, unlinked.Registration!.Version); Assert.Equal(outgoing2.Id, (await f.Db.Set<DocumentRelation>().SingleAsync()).OutgoingDocumentId);
        var versions = (await f.Db.DocumentRegistrations.ToListAsync()).ToDictionary(x => x.DocumentId, x => x.Version);
        var auditCount = await f.Db.Set<DocumentEditAudit>().CountAsync(); var eventCount = await f.Db.DocumentOutboxEvents.CountAsync();
        await Update(f.Db, unlinked, new(RemovedIds: [outgoing1.Id]), Scope(incoming.Id, outgoing1.Id));
        Assert.Equal(auditCount, await f.Db.Set<DocumentEditAudit>().CountAsync()); Assert.Equal(eventCount, await f.Db.DocumentOutboxEvents.CountAsync());
        Assert.Equal(versions, (await f.Db.DocumentRegistrations.ToListAsync()).ToDictionary(x => x.DocumentId, x => x.Version));
    }

    [Fact]
    public async Task Registration_with_relations_normalizes_order_and_replays_without_touching_counterparts_again()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var a = await Register(f.Db, "OUTGOING"); var b = await Register(f.Db, "OUTGOING");
        var scope = Scope(a.Id, b.Id); var first = await Register(f.Db, "INCOMING", [b.Id, a.Id], scope, "related-register");
        Assert.Equal(2, await f.Db.Set<DocumentRelation>().CountAsync()); Assert.Equal(1, first.Registration!.Version);
        Assert.All(await f.Db.DocumentRegistrations.Where(x => x.Kind == "OUTGOING").ToListAsync(), x => Assert.Equal(2, x.Version));
        var replay = await Register(f.Db, "INCOMING", [a.Id, b.Id], scope, "related-register"); Assert.Equal(first.Id, replay.Id);
        Assert.Equal(2, await f.Db.Set<DocumentEditAudit>().CountAsync()); Assert.Equal(5, await f.Db.DocumentOutboxEvents.CountAsync());
        Assert.Equal(3, (await f.Db.DocumentNumberCounters.ToListAsync()).Sum(x => x.CurrentValue));
    }

    [Theory]
    [InlineData("noScope")][InlineData("wrongActor")][InlineData("unreadable")][InlineData("internalSource")][InlineData("sameKind")]
    [InlineData("internalTarget")][InlineData("missingTarget")][InlineData("deletedTarget")][InlineData("legacyTarget")]
    [InlineData("self")][InlineData("duplicate")][InlineData("emptyId")][InlineData("contradiction")][InlineData("tooMany")]
    public async Task Invalid_or_unreadable_relation_edits_never_change_a_graph(string invalid)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, invalid == "internalSource" ? "INTERNAL" : "INCOMING");
        var target = await Register(f.Db, invalid == "sameKind" ? "INCOMING" : invalid == "internalTarget" ? "INTERNAL" : "OUTGOING");
        if (invalid == "legacyTarget") { f.Db.DocumentRegistrations.Remove(target.Registration!); await f.Db.SaveChangesAsync(); }
        if (invalid == "deletedTarget") { target.IsDeleted = true; await f.Db.SaveChangesAsync(); }
        var ids = invalid == "self" ? new[] { source.Id } : invalid == "missingTarget" ? new[] { Guid.NewGuid() } : new[] { target.Id };
        var change = new V2RelationChange(ids); V2RelationScope? scope = Scope(source.Id, ids[0]);
        if (invalid == "duplicate") change = new([target.Id, target.Id]);
        if (invalid == "emptyId") change = new([Guid.Empty]);
        if (invalid == "contradiction") change = new([target.Id], [target.Id]);
        if (invalid == "tooMany") change = new(Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray());
        if (invalid == "noScope") scope = null;
        if (invalid == "wrongActor") scope = scope! with { ActorUserId = Guid.NewGuid() };
        if (invalid == "unreadable") scope = Scope(source.Id);
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Update(f.Db, source, change, scope));
        Assert.Contains(error.Status, new[] { 400, 403, 404 }); Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync());
        Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync()); Assert.All(await f.Db.DocumentRegistrations.ToListAsync(), h => Assert.Equal(1, h.Version));
        Assert.Equal(2, await f.Db.DocumentOutboxEvents.CountAsync());
    }

    [Fact]
    public async Task Source_edit_authority_does_not_imply_counterpart_edit_authority_or_bypass_its_read_scope()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, "INCOMING");
        var otherIdentity = new RegistrationIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FIN", "Finance");
        var target = await Register(f.Db, "OUTGOING", identity: otherIdentity);
        await Update(f.Db, source, new([target.Id]), Scope(source.Id, target.Id));
        var header = await f.Db.DocumentRegistrations.SingleAsync(x => x.DocumentId == target.Id);
        Assert.Equal(otherIdentity.InputterUserId, header.InputterUserId); Assert.Equal(otherIdentity.OriginatorUserId, header.OriginatorUserId);
        var e = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(target.Id,
            Edit(target, null) with { ExpectedVersion = 2, Subject = "Forbidden" }, V2EditingTests.Actor()));
        Assert.Equal(403, e.Status); Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
    }

    [Fact]
    public async Task Counterpart_metadata_with_a_pre_link_version_is_stale()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var source = await Register(f.Db, "INCOMING"); var target = await Register(f.Db, "OUTGOING");
        var stale = Edit(target, null) with { Subject = "Stale" }; await Update(f.Db, source, new([target.Id]), Scope(source.Id, target.Id));
        var e = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(target.Id, stale, V2EditingTests.Actor()));
        Assert.Equal(409, e.Status); Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
    }

    [Fact]
    public async Task Source_audit_failure_rolls_back_edge_and_both_versions()
    {
        var fault = new V2EditingTests.AuditFailure(); await using var f = await V2EditingTests.Fixture.Create(fault);
        var source = await Register(f.Db, "INCOMING"); var target = await Register(f.Db, "OUTGOING");
        await Assert.ThrowsAsync<IOException>(() => Update(f.Db, source, new([target.Id]), Scope(source.Id, target.Id)));
        Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync()); Assert.All(await f.Db.DocumentRegistrations.ToListAsync(), x => Assert.Equal(1, x.Version));
        Assert.Equal(2, await f.Db.DocumentOutboxEvents.CountAsync()); Assert.False(f.Db.ChangeTracker.HasChanges());
        fault.Enabled = false; await Update(f.Db, source, new([target.Id]), Scope(source.Id, target.Id)); Assert.Single(await f.Db.Set<DocumentRelation>().ToListAsync());
    }

    [Theory]
    [InlineData("unreadable")][InlineData("wrongKind")][InlineData("wrongActor")]
    public async Task Failed_related_registration_does_not_allocate_or_touch_existing_headers(string invalid)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var target = await Register(f.Db, invalid == "wrongKind" ? "INCOMING" : "OUTGOING");
        var scope = invalid == "unreadable" ? Scope() : Scope(target.Id); if (invalid == "wrongActor") scope = scope with { ActorUserId = Guid.NewGuid() };
        await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Register(f.Db, "INCOMING", [target.Id], scope));
        Assert.Single(await f.Db.Documents.ToListAsync()); Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Equal(1, (await f.Db.DocumentRegistrations.SingleAsync()).Version); Assert.Empty(await f.Db.Set<DocumentRelation>().ToListAsync());
    }
}
