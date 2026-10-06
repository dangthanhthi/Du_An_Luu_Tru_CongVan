using Das.PdfProtocol;
using Microsoft.EntityFrameworkCore;
namespace DocumentService;

// Supplied by a trusted provisioning adapter, never by an HTTP body or generic role.
public sealed record ReportAuthority(Guid UserId,bool IsActive,bool CanReport,bool CanExport,
    bool OrganizationWide,IReadOnlySet<Guid> DepartmentIds,IReadOnlySet<Guid> ManagedStaffIds,
    IReadOnlySet<Guid> ConfidentialDocumentIds);
public interface IReportAuthority { Task<ReportAuthority> ResolveAsync(Guid user,CancellationToken ct); }
public sealed class UnavailableReportAuthority:IReportAuthority
{ public Task<ReportAuthority> ResolveAsync(Guid user,CancellationToken ct)=>throw new DocumentRegistrationRuleException(503,"REPORT_AUTHORITY_UNAVAILABLE","Report authority is not connected."); }
public sealed record IncompleteFilter(string? Kind=null,Guid? DepartmentId=null,bool IncludeRecent=false);
public sealed record IncompleteRow(Guid DocumentId,string Kind,Guid DepartmentId,string DepartmentName,
    bool HasAttachment,string RegistrationNumber,DateOnly RegisteredDate,DateOnly? IssueDate,
    Guid Originator,string Status,IReadOnlyList<string> RecipientList,long Version);
public sealed record ReportGroup(Guid DepartmentId,string DepartmentName,int Count);
public sealed record IncompleteReport(IReadOnlyList<IncompleteRow> Items,int Total,int PageNumber,int PageSize,
    IReadOnlyList<ReportGroup> Groups,DateTimeOffset EvaluatedAt,bool CanExport);

public sealed class CurrentPdfAvailability(DocumentDbContext db,IPdfFilesClient files)
{
    public async Task<bool> HasAsync(Guid id,CancellationToken ct)
    {
        var link=await db.Set<DocumentCurrentPdf>().AsNoTracking().SingleOrDefaultAsync(x=>x.DocumentId==id,ct);
        if(link is null||link.State!="Ready")return false;
        PdfReceipt receipt;
        try { receipt=await files.InspectAsync(link.OperationId,ct); }
        catch(PdfProtocolException e) when(e.Status is 404 or 410 or 423) {return false;}
        catch(Exception e) when(e is HttpRequestException or IOException) {throw new DocumentRegistrationRuleException(503,"PDF_DEPENDENCY_UNAVAILABLE","PDF verification is unavailable.");}
        if(receipt.OperationId!=link.OperationId||receipt.DocumentId!=id||receipt.FileId!=link.FileId||receipt.Sha256!=link.Sha256||receipt.SizeBytes!=link.SizeBytes)
            throw new DocumentRegistrationRuleException(503,"PDF_RECEIPT_CONFLICT","PDF verification is inconsistent.");
        // A concurrent replacement invalidates the proof, even if its header is unchanged.
        if(!await db.Set<DocumentCurrentPdf>().AnyAsync(x=>x.DocumentId==id&&x.Version==link.Version&&x.OperationId==link.OperationId&&x.State=="Ready",ct))
            throw new DocumentRegistrationRuleException(409,"REPORT_CHANGED","The report changed; reload it.");
        return receipt.State=="Active";
    }
}

public sealed class IncompleteReports(DocumentDbContext db,CurrentPdfAvailability pdf,TimeProvider clock)
{
    public const int RowBudget=1000;
    public async Task<IncompleteReport> QueryAsync(Guid user,ReportAuthority scope,IncompleteFilter filter,int page,int size,bool export,CancellationToken ct)
    {
        if(scope.UserId!=user)throw Rule(503,"AUTHORITY_MISMATCH");
        if(!scope.IsActive||!scope.CanReport||export&&!scope.CanExport)throw Rule(403,"REPORT_FORBIDDEN");
        if(page<1||page>1000000||size<1||size>100||filter.Kind is not (null or "OUTGOING" or "INTERNAL")||filter.DepartmentId==Guid.Empty)throw Rule(400,"INVALID_REPORT_QUERY");
        var departments=scope.DepartmentIds.ToArray();var staff=scope.ManagedStaffIds.ToArray();var confidential=scope.ConfidentialDocumentIds.ToArray();
        var query=db.DocumentRegistrations.AsNoTracking().Where(x=>x.Document!=null&&(x.Kind=="OUTGOING"||x.Kind=="INTERNAL")&&x.Document.DocType==x.Kind&&x.Document.Status!="Cancelled")
            .Where(x=>scope.OrganizationWide||departments.Contains(x.OwnerDepartmentId)||staff.Contains(x.InputterUserId)||staff.Contains(x.OriginatorUserId))
            .Where(x=>x.Sensitivity=="Normal"||confidential.Contains(x.DocumentId));
        if(filter.Kind is not null)query=query.Where(x=>x.Kind==filter.Kind);
        if(filter.DepartmentId is not null)query=query.Where(x=>x.OwnerDepartmentId==filter.DepartmentId);
        var now=clock.GetUtcNow();var cutoff=DocumentNumberFormatter.RegistrationDate(now).AddDays(-14);
        if(!filter.IncludeRecent)query=query.Where(x=>x.RegistrationDate<cutoff);
        if(await query.CountAsync(ct)>RowBudget)throw Rule(422,"REPORT_SCOPE_TOO_LARGE");
        var headers=await query.Include(x=>x.Document!).ThenInclude(x=>x.Recipients).OrderBy(x=>x.OwnerDepartmentNameSnapshot).ThenBy(x=>x.OwnerDepartmentId).ThenBy(x=>x.RegistrationDate).ThenBy(x=>x.SequenceNumber).ThenBy(x=>x.DocumentId).ToListAsync(ct);
        var rows=new List<IncompleteRow>();
        foreach(var h in headers)
        {
            var available=await pdf.HasAsync(h.DocumentId,ct);
            if(DocumentCompletionEvaluator.Evaluate(h.Document!.Status,available,h.IssuedDate).IsComplete)continue;
            rows.Add(new(h.DocumentId,V2HttpKinds.Display(h.Kind),h.OwnerDepartmentId,h.OwnerDepartmentNameSnapshot,available,h.Document.DocumentNumber,h.RegistrationDate,h.IssuedDate,h.OriginatorUserId,h.Document.Status,
                h.Kind=="OUTGOING"?h.Document.Recipients.OrderBy(x=>x.ReferenceId).Select(x=>x.NameSnapshot).ToArray():[],h.Version));
        }
        // Header mutations while verifying storage must not yield a mixed snapshot.
        var ids=headers.Select(x=>x.DocumentId).ToArray();
        var versions=await db.DocumentRegistrations.AsNoTracking().Where(x=>ids.Contains(x.DocumentId)&&x.Document!=null).ToDictionaryAsync(x=>x.DocumentId,x=>x.Version,ct);
        if(headers.Any(x=>versions.GetValueOrDefault(x.DocumentId)!=x.Version))throw Rule(409,"REPORT_CHANGED");
        var groups=rows.GroupBy(x=>x.DepartmentId).Select(g=>new ReportGroup(g.Key,g.First().DepartmentName,g.Count())).ToArray();
        return new(export?rows.ToArray():rows.Skip(checked((page-1)*size)).Take(size).ToArray(),rows.Count,page,size,groups,now,scope.CanExport);
    }
    private static DocumentRegistrationRuleException Rule(int status,string code)=>new(status,code,code);
}
