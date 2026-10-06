namespace DocumentService;

public enum V2StatusAction { Distribute, Cancel, Restore }
public sealed record V2StatusDraft(long ExpectedVersion, V2StatusAction Action, string? Reason = null);
