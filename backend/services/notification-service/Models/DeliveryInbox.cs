namespace NotificationService.Models;
public sealed class DeliveryInbox
{
    public Guid Id {get;set;}=Guid.NewGuid();public Guid SenderId {get;set;}
    public string KeyHash {get;set;}="";public string BodyHash {get;set;}="";public string PayloadJson {get;set;}="";
    public string State {get;set;}="Queued";public int Attempts {get;set;}public Guid? LeaseToken {get;set;}
    public long LeaseUntilUnix {get;set;}public long NextAttemptUnix {get;set;}public long Version {get;set;}=1;
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
