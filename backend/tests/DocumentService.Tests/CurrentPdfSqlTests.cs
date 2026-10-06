using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;
public sealed class CurrentPdfSqlTests
{
    [CatalogSqlFact]
    public async Task Parallel_pdf_replacements_share_version_lock_and_have_one_current_winner()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();Document d;
        await using(var db=f.Db())d=await V2LifecycleTests.Register(db);
        var results=await Task.WhenAll(Enumerable.Range(0,16).Select(async _=> {
            await using var db=f.Db();try {await new CurrentPdfService(db,new CurrentPdfTests.Files(),V2EditingTests.Clock).ReplaceAsync(d.Id,CurrentPdfTests.Draft(d),V2EditingTests.Actor());return 200;}
            catch(DocumentRegistrationRuleException e){return e.Status;}
        }));
        Assert.Equal(1,results.Count(x=>x==200));Assert.Equal(15,results.Count(x=>x==409));await using var check=f.Db();Assert.Single(await check.Set<DocumentCurrentPdf>().ToListAsync());Assert.Single(await check.Set<DocumentEditAudit>().ToListAsync());Assert.Equal(2,(await check.DocumentRegistrations.SingleAsync()).Version);
    }
    [CatalogSqlFact]
    public async Task Actual_sql_audit_constraint_rolls_back_pdf_link_but_keeps_preparing_receipt()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create();await using var db=f.Db();var doc=await V2LifecycleTests.Register(db);
        db.Set<DocumentEditAudit>().Add(new(){DocumentId=doc.Id,Version=2,ActorUserId=V2EditingTests.Actor().UserId,ChangedAt=V2EditingTests.Clock.GetUtcNow(),ChangesJson="{}"});await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(()=>new CurrentPdfService(db,new CurrentPdfTests.Files(),V2EditingTests.Clock).ReplaceAsync(doc.Id,CurrentPdfTests.Draft(doc),V2EditingTests.Actor()));
        Assert.Empty(await db.Set<DocumentCurrentPdf>().ToListAsync());Assert.Equal("Preparing",(await db.Set<PdfReplacement>().SingleAsync()).State);Assert.Equal(1,(await db.DocumentRegistrations.SingleAsync()).Version);Assert.Single(await db.DocumentOutboxEvents.ToListAsync());
    }
    [CatalogSqlFact]
    public async Task Additive_migration_keeps_existing_v2_documents_and_legacy_attachment_metadata()
    {
        await using var f=await RegistrationSqlTests.Fixture.Create("20261004134524_AddDocumentCancellations");await using var db=f.Db();var doc=await V2LifecycleTests.Register(db);var attachment=new DocumentAttachment{DocumentId=doc.Id,FileId=Guid.NewGuid(),AttachmentType="PDF"};db.Add(attachment);await db.SaveChangesAsync();
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();Assert.Empty(await db.Set<DocumentCurrentPdf>().ToListAsync());Assert.Empty(await db.Set<PdfReplacement>().ToListAsync());Assert.Equal(attachment.FileId,(await db.DocumentAttachments.SingleAsync()).FileId);
        Assert.Equal(1,(await db.DocumentRegistrations.SingleAsync()).Version);Assert.Single(await db.RegistrationRequests.ToListAsync());
    }
}
