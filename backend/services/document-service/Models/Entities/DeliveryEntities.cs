namespace DocumentService;

public sealed class ReminderBatch
{
    public Guid Id {get;set;}=Guid.NewGuid();public Guid DepartmentId {get;set;}public DateOnly Period {get;set;}
    public string State {get;set;}="Planned";public string PayloadJson {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}public long LeaseUntilUnix {get;set;}public Guid? LeaseToken {get;set;}
    public int Attempts {get;set;}public long Version {get;set;}=1;public string? ErrorCode {get;set;}
}

public sealed class ReminderFanoutManifest {public Guid BatchId{get;set;}public string PlanHash{get;set;}="";}
public sealed class ReminderDelivery
{
    public Guid Id{get;set;}=Guid.NewGuid();public Guid BatchId{get;set;}public Guid InputterUserId{get;set;}
    public string PayloadJson{get;set;}="";public string State{get;set;}="Pending";public int Attempts{get;set;}public int Failures{get;set;}
    public long LeaseUntilUnix{get;set;}public Guid? LeaseToken{get;set;}public long NextAttemptUnix{get;set;}public long Version{get;set;}=1;
    public Guid? NotificationId{get;set;}public string? NotificationState{get;set;}
}

public sealed class DocumentTaskIntent
{
    public Guid Id{get;set;}=Guid.NewGuid();public Guid DocumentId{get;set;}public Guid ActorId{get;set;}public Guid AssigneeId{get;set;}
    public string Title{get;set;}="";public string KeyHash{get;set;}="";public string BodyHash{get;set;}="";
    public string State{get;set;}="PendingConfiguration";public string? RemoteTaskId{get;set;}public long LeaseUntilUnix{get;set;}public Guid? LeaseToken{get;set;}public long Version{get;set;}=1;
}

public sealed class DocumentNotificationDelivery
{
    public Guid Id{get;set;}=Guid.NewGuid();public Guid EventId{get;set;}public Guid RecipientId{get;set;}
    public string State{get;set;}="Pending";public int Attempts{get;set;}public long NextAttemptUnix{get;set;}public long LeaseUntilUnix{get;set;}public Guid? LeaseToken{get;set;}
    public string PayloadJson{get;set;}="";public long Version{get;set;}=1;
}
