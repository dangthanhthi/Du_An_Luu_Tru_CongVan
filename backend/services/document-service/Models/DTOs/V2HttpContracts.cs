using System.Text.Json.Serialization;
namespace DocumentService;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterDocumentRequest(string Kind,string CompanyCode,string Subject,Guid OriginatorUserId,
    Guid OwnerDepartmentId,string Sensitivity="Normal",DateOnly? IssuedDate=null,string? Remark=null,
    V2KindDetailsDraft? Details=null,IReadOnlyList<Guid>? RelatedDocumentIds=null)
{
    public V2RegistrationDraft Draft()=>new(V2HttpKinds.Parse(Kind),CompanyCode,Subject,OriginatorUserId,OwnerDepartmentId,Sensitivity,IssuedDate,Remark,Details,RelatedDocumentIds);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditDocumentRequest(long ExpectedVersion,string CompanyCode,string Subject,Guid OriginatorUserId,
    Guid OwnerDepartmentId,string Sensitivity,DateOnly? IssuedDate=null,string? Remark=null,
    V2KindDetailsDraft? Details=null,V2RelationChange? Relations=null)
{
    public V2EditDraft Draft()=>new(ExpectedVersion,CompanyCode,Subject,OriginatorUserId,OwnerDepartmentId,Sensitivity,IssuedDate,Remark,Details,Relations);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeDocumentStatusRequest(long ExpectedVersion,string Action,string? Reason=null)
{
    public V2StatusDraft Draft()=>new(ExpectedVersion,Action switch {"Distribute"=>V2StatusAction.Distribute,"Cancel"=>V2StatusAction.Cancel,"Restore"=>V2StatusAction.Restore,_=>throw new DocumentRegistrationRuleException(400,"INVALID_STATUS_INTENT","Invalid status action.")},Reason);
}
public sealed record V2WriteResult(Guid Id,string RegistrationNumber,long Version,string Status);
public sealed record V2DocumentRow(Guid Id,string Kind,string RegistrationNumber,string Subject,string Status,
    DateOnly RegistrationDate,DateOnly? IssuedDate,string CompanyCode,string DepartmentName,string Sensitivity,
    string? ReferenceNumber,string? SenderPartnerName,long Version,IReadOnlyList<string> AllowedActions,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] bool? IsComplete);
public sealed record V2DocumentDetail(V2DocumentRow Header,Guid OriginatorUserId,Guid OwnerDepartmentId,
    Guid InputterUserId,Guid LastModifierUserId,string? Remark,DocumentKindDetails? Details,
    IReadOnlyList<V2RecipientRow> Recipients,IReadOnlyList<Guid> RelatedDocumentIds,string PdfState);
public sealed record V2RecipientRow(string ReferenceType,Guid ReferenceId,string Name);
public sealed record V2DocumentPage(IReadOnlyList<V2DocumentRow> Items,int TotalCount,int PageNumber,int PageSize);
internal static class V2HttpKinds
{
    public static string Parse(string? kind)=>kind switch {"Incoming" or "INCOMING"=>"INCOMING","Outgoing" or "OUTGOING"=>"OUTGOING","Internal" or "INTERNAL"=>"INTERNAL",_=>throw new DocumentRegistrationRuleException(400,"INVALID_KIND","Invalid document kind.")};
    public static string Display(string kind)=>kind switch {"INCOMING"=>"Incoming","OUTGOING"=>"Outgoing","INTERNAL"=>"Internal",_=>throw new DocumentRegistrationRuleException(409,"REGISTRATION_INCONSISTENT","Invalid stored kind.")};
}
