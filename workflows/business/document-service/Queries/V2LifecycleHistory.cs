using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public sealed class V2LifecycleHistory(DocumentDbContext db)
{
    public async Task<V2LifecycleHistoryPage> ReadAsync(Guid id,int page,int size,long? watermark,V2ReadAuthority scope,CancellationToken ct)
    {
        if (!scope.ReadableDocumentIds.Contains(id)) throw Missing();
        if(page<1 || page>1_000_000 || size<1 || size>100 || watermark is <=0 || (page>1 && watermark is null)) throw InvalidQuery();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var header=await db.DocumentRegistrations.AsNoTracking().Where(h=>h.DocumentId==id && h.Document!=null && h.Document.DocType==h.Kind &&
                (h.Kind=="INTERNAL" || h.Kind=="OUTGOING" || h.Kind=="INCOMING")).Select(h=>new {h.Version}).SingleOrDefaultAsync(ct) ?? throw Missing();
            if(header.Version<1) throw V2LifecycleHistoryParser.Invalid();
            if(watermark>header.Version) throw InvalidQuery(); var through=watermark??header.Version;
            // Parse before count and paging: metadata edits are deliberately excluded, invalid events cannot be hidden on another page.
            var audits=db.Set<DocumentEditAudit>().AsNoTracking().Where(x=>x.DocumentId==id && x.Version<=through)
                .OrderByDescending(x=>x.Version).ThenBy(x=>x.Id).AsAsyncEnumerable();
            var items=new List<V2LifecycleHistoryItem>(); var count=0; var offset=(page-1)*size;
            await foreach(var audit in audits.WithCancellation(ct)) {
                var item=V2LifecycleHistoryParser.Parse(audit); if(item is null) continue;
                if(count>=offset && items.Count<size) items.Add(item);
                count=checked(count+1);
            }
            var result=new V2LifecycleHistoryPage(id,through.ToString(CultureInfo.InvariantCulture),items,count,page,size);
            await transaction.CommitAsync(ct); return result;
        });
    }
    internal static DocumentRegistrationRuleException Missing()=>new(404,"DOCUMENT_NOT_FOUND","The document is unavailable.");
    internal static DocumentRegistrationRuleException InvalidQuery()=>new(400,"INVALID_HISTORY_QUERY","The history query is invalid.");
}
