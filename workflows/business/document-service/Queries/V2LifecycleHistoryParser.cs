using System.Globalization;
using System.Text.Json;

namespace DocumentService;

internal static class V2LifecycleHistoryParser
{
    private sealed record Snapshot(string PreviousStatus,string Reason,Guid CancelledByUserId,DateTimeOffset CancelledAt,Guid? RestoredByUserId,DateTimeOffset? RestoredAt);
    internal static V2LifecycleHistoryItem? Parse(DocumentEditAudit audit)
    {
        if (audit.Id==Guid.Empty || audit.ActorUserId==Guid.Empty || audit.Version<2 || audit.ChangedAt==default || audit.ChangedAt.Offset!=TimeSpan.Zero ||
            string.IsNullOrEmpty(audit.ChangesJson) || audit.ChangesJson.Length>1_048_576) throw Invalid();
        try {
            // Field edits can include two snapshots of 200 recipient names and long Unicode fields.
            // Allow their verified writer bounds, then apply the smaller lifecycle bound after recognizing Status.
            using var json=JsonDocument.Parse(audit.ChangesJson,new JsonDocumentOptions {MaxDepth=8}); var root=json.RootElement;
            Object(root);
            if (!root.TryGetProperty("Status",out var status)) {
                if(root.TryGetProperty("Cancellation",out _)) throw Invalid();
                return null; // A valid field/PDF/relation audit is outside lifecycle coverage.
            }
            if(audit.ChangesJson.Length>65536) throw Invalid();
            Exact(root,"Status","Cancellation"); Exact(status,"before","after");
            var from=Text(status.GetProperty("before")); var to=Text(status.GetProperty("after"));
            var cancellation=root.GetProperty("Cancellation"); Exact(cancellation,"before","after");
            var before=ReadSnapshot(cancellation.GetProperty("before")); var after=ReadSnapshot(cancellation.GetProperty("after"));
            string action; string? reason;
            if(from=="InProgress" && to=="Distributed") {
                if(before!=after || before is {RestoredAt:null}) throw Invalid();
                action="Distribute"; reason=null;
            } else if(from is "InProgress" or "Distributed" && to=="Cancelled") {
                if(before is {RestoredAt:null} || after is null || after.PreviousStatus!=from || after.RestoredAt is not null ||
                    after.CancelledByUserId!=audit.ActorUserId || after.CancelledAt!=audit.ChangedAt) throw Invalid();
                action="Cancel"; reason=after.Reason;
            } else if(from=="Cancelled" && to is "InProgress" or "Distributed") {
                if(before is null || before.RestoredAt is not null || before.PreviousStatus!=to || after is null ||
                    after.PreviousStatus!=before.PreviousStatus || after.Reason!=before.Reason || after.CancelledAt!=before.CancelledAt ||
                    after.CancelledByUserId!=before.CancelledByUserId || after.RestoredByUserId!=audit.ActorUserId || after.RestoredAt!=audit.ChangedAt) throw Invalid();
                action="Restore"; reason=before.Reason;
            } else throw Invalid();
            return new(audit.Id,audit.ActorUserId,audit.Version.ToString(CultureInfo.InvariantCulture),audit.ChangedAt.UtcDateTime,action,from,to,reason);
        }
        catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) {throw Invalid();}
    }
    private static Snapshot? ReadSnapshot(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Null) return null;
        Exact(value,"PreviousStatus","Reason","CancelledByUserId","CancelledAt","RestoredByUserId","RestoredAt");
        var previous=Text(value.GetProperty("PreviousStatus")); var reason=Text(value.GetProperty("Reason"));
        if(previous is not ("InProgress" or "Distributed") || string.IsNullOrWhiteSpace(reason) || reason.Length>4000) throw Invalid();
        var actor=GuidValue(value.GetProperty("CancelledByUserId")); var time=Utc(value.GetProperty("CancelledAt"));
        var restorer=value.GetProperty("RestoredByUserId"); var restored=value.GetProperty("RestoredAt");
        if((restorer.ValueKind==JsonValueKind.Null)!=(restored.ValueKind==JsonValueKind.Null)) throw Invalid();
        Guid? restoreActor=restorer.ValueKind==JsonValueKind.Null?null:GuidValue(restorer);
        DateTimeOffset? restoreTime=restored.ValueKind==JsonValueKind.Null?null:Utc(restored);
        if(restoreTime<time) throw Invalid();
        return new(previous,reason,actor,time,restoreActor,restoreTime);
    }
    private static void Object(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object) throw Invalid();
        var names=value.EnumerateObject().Select(x=>x.Name).ToArray();
        if(names.Distinct(StringComparer.Ordinal).Count()!=names.Length) throw Invalid();
    }
    private static void Exact(JsonElement value,params string[] names)
    {Object(value); if(!value.EnumerateObject().Select(x=>x.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal))) throw Invalid();}
    private static string Text(JsonElement value)=>value.ValueKind==JsonValueKind.String?value.GetString()!:throw Invalid();
    private static Guid GuidValue(JsonElement value)=>value.ValueKind==JsonValueKind.String && value.TryGetGuid(out var id) && id!=Guid.Empty?id:throw Invalid();
    private static DateTimeOffset Utc(JsonElement value)=>value.ValueKind==JsonValueKind.String && value.TryGetDateTimeOffset(out var time) && time!=default && time.Offset==TimeSpan.Zero?time:throw Invalid();
    internal static DocumentRegistrationRuleException Invalid()=>new(503,"HISTORY_DATA_INVALID","The stored history cannot be verified.");
}
