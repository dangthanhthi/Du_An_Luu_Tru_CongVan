using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2KindDetailsSqlTests
{
    private static Task<Document> Register(DocumentDbContext db, string key = "details-sql") =>
        new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(
            V2PersistenceTests.Draft() with { Details = V2KindDetailsTests.Outgoing() }, V2PersistenceTests.Identity, key,
            references: V2KindDetailsTests.References());

    [CatalogSqlFact]
    public async Task Concurrent_registration_replays_one_complete_recipient_graph_and_one_allocation()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create();
        var documents = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ => { await using var db = f.Db(); return await Register(db); }));
        Assert.Single(documents.Select(x => x.Id).Distinct());
        Assert.All(documents, d => { Assert.NotNull(d.KindDetails); Assert.Equal(2, d.Recipients.Count); });
        await using var verify = f.Db(); Assert.Equal(1, await verify.Set<DocumentKindDetails>().CountAsync());
        Assert.Equal(2, await verify.Set<DocumentRecipient>().CountAsync()); Assert.Single(await verify.DocumentOutboxEvents.ToListAsync());
        Assert.Equal(1, (await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Same_version_edit_race_keeps_one_header_details_and_recipient_set()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); Guid id; V2EditDraft draft;
        await using (var db = f.Db())
        {
            var doc = await Register(db); id = doc.Id;
            draft = V2KindDetailsTests.Header(doc, V2KindDetailsTests.Outgoing() with { RecipientPartnerIds = [V2KindDetailsTests.PartnerA] });
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(async i => {
            await using var db = f.Db();
            try
            {
                await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(id, draft with { Subject = "Winner " + i,
                    Details = draft.Details! with { ContractNumber = "Contract " + i } }, V2EditingTests.Actor());
                return 200;
            }
            catch (DocumentRegistrationRuleException error) { return error.Status; }
        }));
        Assert.Equal(1, results.Count(x => x == 200)); Assert.Equal(15, results.Count(x => x == 409));
        await using var verify = f.Db(); var title = (await verify.Documents.SingleAsync()).Title;
        Assert.Equal("Contract " + title[7..], (await verify.Set<DocumentKindDetails>().SingleAsync()).ContractNumber);
        Assert.Equal(V2KindDetailsTests.PartnerA, (await verify.Set<DocumentRecipient>().SingleAsync()).ReferenceId);
        Assert.Equal(2, (await verify.DocumentRegistrations.SingleAsync()).Version); Assert.Single(await verify.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(2, await verify.DocumentOutboxEvents.CountAsync()); Assert.Equal(1, (await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Real_SQL_constraint_failure_rolls_back_recipient_deletion_and_metadata()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); await using var db = f.Db(); var doc = await Register(db);
        db.Set<DocumentEditAudit>().Add(new() { DocumentId = doc.Id, ActorUserId = V2EditingTests.Actor().UserId, Version = 2,
            ChangedAt = V2EditingTests.Clock.GetUtcNow(), ChangesJson = "{}" }); await db.SaveChangesAsync();
        var draft = V2KindDetailsTests.Header(doc, V2KindDetailsTests.Outgoing() with { RecipientPartnerIds = [], MethodCode = "FAX" }) with { Subject = "Uncommitted", CompanyCode = "HV" };
        await Assert.ThrowsAsync<DbUpdateException>(() => new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(doc.Id, draft, V2EditingTests.Actor()));
        Assert.False(db.ChangeTracker.HasChanges()); await using var verify = f.Db();
        Assert.Equal(2, await verify.Set<DocumentRecipient>().CountAsync()); Assert.Equal("EMAIL", (await verify.Set<DocumentKindDetails>().SingleAsync()).MethodCode);
        Assert.Equal("27-01-0001/HL/ADM", (await verify.Documents.SingleAsync()).DocumentNumber);
        Assert.Equal(1, (await verify.DocumentRegistrations.SingleAsync()).Version); Assert.Single(await verify.DocumentOutboxEvents.ToListAsync());
        Assert.Equal(1, (await verify.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Additive_details_migration_keeps_prior_header_receipt_and_legacy_data()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create("20261004122637_AddDocumentEditAudit"); await using var db = f.Db();
        var doc = await new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(), V2PersistenceTests.Identity, "before-details");
        var receipt = await db.RegistrationRequests.SingleAsync(); var bodyHash = receipt.BodyHash;
        var legacy = new Document { DocType = "INTERNAL", Title = "Legacy", Status = "Reviewed", DocumentNumber = "20-12-0112/INT/HL/HSE", CreatedByUserId = Guid.NewGuid() };
        db.Documents.Add(legacy); await db.SaveChangesAsync(); await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Empty(await db.Set<DocumentKindDetails>().ToListAsync()); Assert.Empty(await db.Set<DocumentRecipient>().ToListAsync());
        Assert.Equal(bodyHash, (await db.RegistrationRequests.SingleAsync()).BodyHash);
        Assert.Equal("Reviewed", (await db.Documents.SingleAsync(x => x.Id == legacy.Id)).Status);
        Assert.Equal("20-12-0112/INT/HL/HSE", (await db.Documents.SingleAsync(x => x.Id == legacy.Id)).DocumentNumber);
        var replay = await new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft(), V2PersistenceTests.Identity, "before-details");
        Assert.Equal(doc.Id, replay.Id); Assert.Null(replay.KindDetails);
        var updated = await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(doc.Id, V2KindDetailsTests.Header(replay, V2KindDetailsTests.Outgoing()), V2EditingTests.Actor(), references: V2KindDetailsTests.References());
        Assert.Equal(2, updated.Registration!.Version); Assert.Equal(2, await db.Set<DocumentRecipient>().CountAsync());
        Assert.Equal(1, (await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [CatalogSqlFact]
    public async Task Large_Unicode_recipient_snapshots_and_full_audit_round_trip_without_truncation()
    {
        await using var f = await RegistrationSqlTests.Fixture.Create(); await using var db = f.Db();
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).Order().ToArray();
        var references = new V2ReferenceSet(ids.ToDictionary(x => x, x => new ExternalEntityReference(x, new string('Đ', 200), true)));
        var details = new V2KindDetailsDraft(MethodCode: "EMAIL", OtherRecipients: new string('ệ', 4000), Others: new string('ư', 4000), RecipientPartnerIds: ids);
        var doc = await new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(V2PersistenceTests.Draft() with { Details = details }, V2PersistenceTests.Identity, "unicode-details", references: references);
        var edited = await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(doc.Id,
            V2KindDetailsTests.Header(doc, details with { RecipientPartnerIds = ids.Take(100).ToArray(), Others = new string('Ừ', 4000) }), V2EditingTests.Actor());
        db.ChangeTracker.Clear(); Assert.Equal(100, await db.Set<DocumentRecipient>().CountAsync());
        Assert.Equal(new string('Ừ', 4000), (await db.Set<DocumentKindDetails>().SingleAsync()).Others);
        using var changes = JsonDocument.Parse((await db.Set<DocumentEditAudit>().SingleAsync()).ChangesJson);
        var before = changes.RootElement.GetProperty("KindDetails").GetProperty("before");
        Assert.Equal(200, before.GetProperty("Recipients").GetArrayLength());
        Assert.Equal(new string('Đ', 200), before.GetProperty("Recipients")[199].GetProperty("NameSnapshot").GetString());
        Assert.Equal(new string('ư', 4000), before.GetProperty("Others").GetString());
        Assert.Equal(2, edited.Registration!.Version);
    }
}
