namespace DocumentService;

public sealed class BusinessCatalogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Group { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}
public sealed class DistributionTarget
{
    public Guid Id { get; set; }
    public int LegacyId { get; set; }
    public string Name { get; set; } = "";
    public string? Initial { get; set; }
    public bool IsActive { get; set; } = true;
    // Importing a label never grants document access. Approval/membership contract
    // must be provided before a target can become mapped.
    public string MappingState { get; set; } = "Pending";
    public long Version { get; set; } = 1;
}
public sealed class CatalogAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

