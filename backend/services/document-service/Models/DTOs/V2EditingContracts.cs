namespace DocumentService;

// Internal application contracts. No browser endpoint accepts these authority snapshots.
public sealed record V2EditorActor(Guid UserId, bool IsActive,
    IReadOnlySet<Guid> DepartmentIds, IReadOnlySet<Guid> LineManagerDepartmentIds,
    IReadOnlySet<Guid> DeputyManagerDepartmentIds);

public sealed record V2EditTarget(Guid OriginatorUserId, Guid OwnerDepartmentId,
    string OwnerDepartmentCode, string OwnerDepartmentName, bool OriginatorIsActive,
    bool DepartmentIsActive);

// Internal edit intent; status and files use separate unfinished use cases.
public sealed record V2EditDraft(long ExpectedVersion, string CompanyCode, string Subject,
    Guid OriginatorUserId, Guid OwnerDepartmentId, string Sensitivity,
    DateOnly? IssuedDate, string? Remark, V2KindDetailsDraft? Details = null, V2RelationChange? Relations = null);

