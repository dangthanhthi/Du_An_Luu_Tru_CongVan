using Microsoft.EntityFrameworkCore;
namespace DocumentService;

public sealed class V2DocumentQueries(DocumentDbContext db)
{
    public async Task<V2DocumentPage> ListAsync(string kind,string view,string? status,string? search,int page,int size,V2ReadAuthority scope,CancellationToken ct)
    {
        var ids=scope.ReadableDocumentIds.ToArray();var departments=scope.Actor.DepartmentIds.ToArray();
        var query=db.DocumentRegistrations.AsNoTracking().Where(h=>ids.Contains(h.DocumentId)&&h.Kind==kind&&h.Document!=null&&h.Document.DocType==h.Kind);
        query=view switch {
            "mine"=>query.Where(h=>h.InputterUserId==scope.Actor.UserId||h.OriginatorUserId==scope.Actor.UserId),
            "department"=>query.Where(h=>departments.Contains(h.OwnerDepartmentId)),
            "cancelled"=>query.Where(h=>h.Document!.Status=="Cancelled"),_=>query};
        if(!string.IsNullOrEmpty(status))query=query.Where(h=>h.Document!.Status==status);
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();query=query.Where(h=>h.Document!.Title.Contains(term)||h.Document.DocumentNumber.Contains(term));}
        var total=await query.CountAsync(ct);
        var headers=await query.Include(h=>h.Document!).ThenInclude(d=>d.KindDetails).OrderByDescending(h=>h.RegistrationDate).ThenByDescending(h=>h.SequenceNumber).ThenBy(h=>h.DocumentId)
            .Skip(checked((page-1)*size)).Take(size).ToListAsync(ct);
        var pageIds=headers.Select(h=>h.DocumentId).ToArray();var pdfs=await db.Set<DocumentCurrentPdf>().AsNoTracking().Where(x=>pageIds.Contains(x.DocumentId)).ToDictionaryAsync(x=>x.DocumentId,x=>x.State,ct);
        return new(headers.Select(h=>Row(h,scope.Actor,pdfs.GetValueOrDefault(h.DocumentId))).ToArray(),total,page,size);
    }
    public async Task<V2DocumentDetail> DetailAsync(Guid id,V2ReadAuthority scope,CancellationToken ct)
    {
        if(!scope.ReadableDocumentIds.Contains(id))throw Missing();
        var h=await db.DocumentRegistrations.AsNoTracking().Include(x=>x.Document!).ThenInclude(d=>d.KindDetails).Include(x=>x.Document!).ThenInclude(d=>d.Recipients).SingleOrDefaultAsync(x=>x.DocumentId==id,ct)??throw Missing();
        if(h.Document is null||h.Document.DocType!=h.Kind)throw Missing();
        var pdf=await db.Set<DocumentCurrentPdf>().AsNoTracking().Where(x=>x.DocumentId==id).Select(x=>x.State).SingleOrDefaultAsync(ct)??"None";
        var relations=await new V2DocumentRelations(db).GetRelatedIdsAsync(id,scope.Actor,new(scope.Actor.UserId,scope.ReadableDocumentIds),ct);
        return new(Row(h,scope.Actor,pdf),h.OriginatorUserId,h.OwnerDepartmentId,h.InputterUserId,h.LastModifierUserId,h.Remark,h.Document.KindDetails,
            h.Document.Recipients.OrderBy(x=>x.ReferenceType).ThenBy(x=>x.ReferenceId).Select(x=>new V2RecipientRow(x.ReferenceType,x.ReferenceId,x.NameSnapshot)).ToArray(),relations,pdf);
    }
    private static V2DocumentRow Row(DocumentRegistration h,V2EditorActor actor,string? pdfState)
    {
        var d=h.Document!;if(d.Status is not ("InProgress" or "Distributed" or "Cancelled"))throw new DocumentRegistrationRuleException(409,"REGISTRATION_INCONSISTENT","Invalid stored status.");
        var actions=new List<string>();if(V2EditAuthority.CanEdit(actor,h)){actions.Add("Edit");actions.Add("ReplacePdf");if(d.Status=="InProgress")actions.Add("Distribute");if(d.Status!="Cancelled")actions.Add("Cancel");}
        if(d.Status=="Cancelled"&&V2EditAuthority.IsParticipant(actor,h))actions.Add("Restore");
        // Ready is a projection, not a fresh byte/scanner proof. Actual readiness is
        // revalidated by GET /pdf and Files content; never mark complete from this row.
        bool? complete=d.Status!="Distributed"||h.IssuedDate is null||pdfState!="Ready"?false:null;
        return new(d.Id,V2HttpKinds.Display(h.Kind),d.DocumentNumber,d.Title,d.Status,h.RegistrationDate,h.IssuedDate,h.CompanyCode,h.OwnerDepartmentNameSnapshot,h.Sensitivity,
            d.KindDetails?.ReferenceNumber,d.KindDetails?.SenderNameSnapshot,h.Version,actions,complete);
    }
    private static DocumentRegistrationRuleException Missing()=>new(404,"DOCUMENT_NOT_FOUND","The document is unavailable.");
}
