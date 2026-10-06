using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2RelationSqlTests
{
    [CatalogSqlFact]
    public async Task Additive_relation_migration_preserves_existing_metadata_receipts_and_historical_documents()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create("20261004125541_AddDocumentKindDetails"); await using var db = f.Db();
        var outgoing = await new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(
            V2PersistenceTests.Draft() with { Details = V2KindDetailsTests.Outgoing() }, V2PersistenceTests.Identity, "before-relations", references: V2KindDetailsTests.References());
        var incoming = await V2RelationTests.Register(db, "INCOMING"); var hash = (await db.RegistrationRequests.SingleAsync(x => x.DocumentId == outgoing.Id)).BodyHash;
        var legacy = new Document { DocType = "INTERNAL", Title = "Historical", Status = "Reviewed", DocumentNumber = "20-12-0112/INT/HL/HSE", CreatedByUserId = Guid.NewGuid() };
        db.Documents.Add(legacy); await db.SaveChangesAsync(); await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Empty(await db.Set<DocumentRelation>().ToListAsync()); Assert.Equal(2, await db.Set<DocumentRecipient>().CountAsync());
        Assert.Equal(hash, (await db.RegistrationRequests.SingleAsync(x => x.DocumentId == outgoing.Id)).BodyHash);
        Assert.Equal("Contract 1", (await db.Set<DocumentKindDetails>().SingleAsync()).ContractNumber);
        Assert.Equal("Reviewed", (await db.Documents.SingleAsync(x => x.Id == legacy.Id)).Status);
        Assert.Equal("20-12-0112/INT/HL/HSE", (await db.Documents.SingleAsync(x => x.Id == legacy.Id)).DocumentNumber);
        var result = await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(outgoing.Id,
            V2RelationTests.Edit(outgoing, new([incoming.Id])), V2EditingTests.Actor(), relationScope: V2RelationTests.Scope(incoming.Id, outgoing.Id));
        Assert.Equal(2, result.Registration!.Version); Assert.Single(await db.Set<DocumentRelation>().ToListAsync());
        Assert.Equal(2, (await db.DocumentNumberCounters.ToListAsync()).Sum(x => x.CurrentValue));
    }
    [CatalogSqlFact]
    public async Task Opposite_endpoint_edits_do_not_deadlock_and_one_old_version_loses()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); Document incoming, outgoing;
        await using (var db = f.Db()) { incoming = await V2RelationTests.Register(db, "INCOMING"); outgoing = await V2RelationTests.Register(db, "OUTGOING"); }
        var scope = V2RelationTests.Scope(incoming.Id, outgoing.Id);
        async Task<int> Link(Document source, Document target)
        {
            await using var db = f.Db();
            try { await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(source.Id, V2RelationTests.Edit(source, new([target.Id])), V2EditingTests.Actor(), relationScope: scope); return 200; }
            catch (DocumentRegistrationRuleException error) { return error.Status; }
        }
        var results = await Task.WhenAll(Link(incoming, outgoing), Link(outgoing, incoming));
        Assert.Equal(1, results.Count(x => x == 200)); Assert.Equal(1, results.Count(x => x == 409));
        await using var verify = f.Db(); Assert.Single(await verify.Set<DocumentRelation>().ToListAsync());
        Assert.All(await verify.DocumentRegistrations.ToListAsync(), x => Assert.Equal(2, x.Version));
        Assert.Equal(2, await verify.Set<DocumentEditAudit>().CountAsync()); Assert.Equal(4, await verify.DocumentOutboxEvents.CountAsync());
        Assert.Equal(outgoing.Id, Assert.Single(await new V2DocumentRelations(verify).GetRelatedIdsAsync(incoming.Id, V2EditingTests.Actor(), scope)));
        Assert.Empty(await new V2DocumentRelations(verify).GetRelatedIdsAsync(incoming.Id, V2EditingTests.Actor(), V2RelationTests.Scope(incoming.Id)));
    }

    [CatalogSqlFact]
    public async Task Parallel_link_and_counterpart_metadata_edit_preserve_both_successful_changes()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); Document source, target;
        await using (var db = f.Db()) { source = await V2RelationTests.Register(db, "INCOMING"); target = await V2RelationTests.Register(db, "OUTGOING"); }
        async Task Link()
        {
            await using var db = f.Db(); await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(source.Id,
                V2RelationTests.Edit(source, new([target.Id])), V2EditingTests.Actor(), relationScope: V2RelationTests.Scope(source.Id, target.Id));
        }
        async Task<int> Metadata()
        {
            await using var db = f.Db();
            try { await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(target.Id,
                V2RelationTests.Edit(target, null) with { Subject = "Concurrent metadata" }, V2EditingTests.Actor()); return 200; }
            catch (DocumentRegistrationRuleException error) { return error.Status; }
        }
        var metadata = Metadata(); await Task.WhenAll(Link(), metadata); var status = await metadata;
        Assert.Contains(status, new[] { 200, 409 }); await using var verify = f.Db(); Assert.Single(await verify.Set<DocumentRelation>().ToListAsync());
        var header = await verify.DocumentRegistrations.Include(x => x.Document).SingleAsync(x => x.DocumentId == target.Id);
        Assert.Equal(status == 200 ? 3 : 2, header.Version); Assert.Equal(status == 200 ? "Concurrent metadata" : "Registered subject", header.Document!.Title);
        Assert.Equal(2, (await verify.DocumentNumberCounters.ToListAsync()).Sum(x => x.CurrentValue));
    }

    [CatalogSqlFact]
    public async Task Related_registration_same_key_creates_one_graph_and_one_counterpart_touch()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); Document target;
        await using (var db = f.Db()) target = await V2RelationTests.Register(db, "OUTGOING");
        var registrations = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ => {
            await using var db = f.Db(); return await V2RelationTests.Register(db, "INCOMING", [target.Id], V2RelationTests.Scope(target.Id), "same-related-key");
        }));
        Assert.Single(registrations.Select(x => x.Id).Distinct()); await using var verify = f.Db();
        Assert.Single(await verify.Set<DocumentRelation>().ToListAsync()); Assert.Single(await verify.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(3, await verify.DocumentOutboxEvents.CountAsync()); Assert.Equal(2, (await verify.DocumentNumberCounters.ToListAsync()).Sum(x => x.CurrentValue));
        Assert.Equal(2, (await verify.DocumentRegistrations.SingleAsync(x => x.DocumentId == target.Id)).Version);
    }

    [CatalogSqlFact]
    public async Task Counterpart_SQL_audit_constraint_failure_rolls_back_source_metadata_and_entire_graph()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); await using var db = f.Db();
        var source = await V2RelationTests.Register(db, "OUTGOING"); var target = await V2RelationTests.Register(db, "INCOMING");
        db.Set<DocumentEditAudit>().Add(new() { DocumentId = target.Id, Version = 2, ActorUserId = V2EditingTests.Actor().UserId,
            ChangedAt = V2EditingTests.Clock.GetUtcNow(), ChangesJson = "{}" }); await db.SaveChangesAsync();
        var draft = V2RelationTests.Edit(source, new([target.Id])) with { CompanyCode = "HV", Subject = "Uncommitted", Details = V2KindDetailsTests.Outgoing() };
        await Assert.ThrowsAsync<DbUpdateException>(() => new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(source.Id,
            draft, V2EditingTests.Actor(), references: V2KindDetailsTests.References(), relationScope: V2RelationTests.Scope(source.Id, target.Id)));
        Assert.False(db.ChangeTracker.HasChanges()); await using var verify = f.Db();
        Assert.Empty(await verify.Set<DocumentRelation>().ToListAsync()); Assert.Empty(await verify.Set<DocumentKindDetails>().ToListAsync());
        Assert.Empty(await verify.Set<DocumentRecipient>().ToListAsync()); Assert.All(await verify.DocumentRegistrations.ToListAsync(), x => Assert.Equal(1, x.Version));
        Assert.Equal("27-01-0001/HL/ADM", (await verify.Documents.SingleAsync(x => x.Id == source.Id)).DocumentNumber);
        Assert.Equal(2, await verify.DocumentOutboxEvents.CountAsync()); Assert.Single(await verify.Set<DocumentEditAudit>().ToListAsync());
    }

    [CatalogSqlFact]
    public async Task Database_guards_refuse_self_edges_and_unregistered_counterparts()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); await using var db = f.Db();
        var source = await V2RelationTests.Register(db, "INCOMING");
        db.Set<DocumentRelation>().Add(new() { IncomingDocumentId = source.Id, OutgoingDocumentId = source.Id, CreatedByUserId = V2EditingTests.Actor().UserId, CreatedAt = V2EditingTests.Clock.GetUtcNow() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var legacy = new Document { DocType = "OUTGOING", Title = "Legacy", DocumentNumber = "20-12-0091/HL/ADM", CreatedByUserId = Guid.NewGuid() };
        db.Documents.Add(legacy); await db.SaveChangesAsync();
        db.Set<DocumentRelation>().Add(new() { IncomingDocumentId = source.Id, OutgoingDocumentId = legacy.Id, CreatedByUserId = V2EditingTests.Actor().UserId, CreatedAt = V2EditingTests.Clock.GetUtcNow() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Empty(await db.Set<DocumentRelation>().ToListAsync()); Assert.Equal(1, (await db.DocumentRegistrations.SingleAsync()).Version);
    }
}
