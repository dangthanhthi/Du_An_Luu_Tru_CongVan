namespace DocumentService;

// Current/latest cancellation; every cycle's immutable evidence remains in audit/history.
public sealed class DocumentCancellation
{
    public Guid DocumentId { get; set; }
    public string PreviousStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public Guid CancelledByUserId { get; set; }
    public DateTimeOffset CancelledAt { get; set; }
    public Guid? RestoredByUserId { get; set; }
    public DateTimeOffset? RestoredAt { get; set; }
}
