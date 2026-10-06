namespace AuthService.Organization;

// Normalized DAS input model; not the unprovisioned EAP organization API schema.
public sealed record OrganizationUnitSnapshot(Guid Id, Guid? ParentId, bool IsDepartment, bool IsActive);
public sealed record DepartmentLeadershipSnapshot(
    Guid DepartmentId, Guid LineManagerUserId, IReadOnlyList<Guid> DeputyLineManagerUserIds);
public sealed record OrganizationValidationError(string Code, Guid? UnitId);
