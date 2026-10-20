namespace DocumentService;

public sealed record V2LifecycleHistoryItem(Guid Id, Guid ActorUserId, string Version, DateTime OccurredAt,
    string Action, string FromStatus, string ToStatus, string? CancellationReason);
public sealed record V2LifecycleHistoryPage(Guid DocumentId, string ThroughVersion, IReadOnlyList<V2LifecycleHistoryItem> Items,
    int TotalCount, int PageNumber, int PageSize, string Coverage = "V2LifecycleOnly");
