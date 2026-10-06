using System.Collections.Immutable;

namespace AuthService.Organization;

// DAS adapter input, not an EAP wire schema. Sequence belongs to the normalized
// full-snapshot stream; an upstream adapter must establish its ordering contract.
public sealed record DirectoryUnit(Guid Id, string Name, string? Code, Guid? ParentId, bool IsDepartment, bool IsActive);
public sealed record DirectoryUser(Guid Id, string DisplayName, bool IsActive);
public sealed record DirectoryMembership(Guid UnitId, Guid UserId, bool IsActive);
public sealed record OrganizationDirectorySnapshot(long Sequence,
    IReadOnlyList<DirectoryUnit> Units, IReadOnlyList<DirectoryUser> Users,
    IReadOnlyList<DirectoryMembership> Memberships, IReadOnlyList<DepartmentLeadershipSnapshot> Leadership);

public sealed record PreparedOrganizationDirectory(long Sequence, string Fingerprint,
    ImmutableArray<DirectoryUnit> Units, ImmutableArray<DirectoryUser> Users,
    ImmutableArray<DirectoryMembership> Memberships, ImmutableArray<DepartmentLeadershipSnapshot> Leadership)
{
    // Membership of a group resolves to its owning Department, never a new department.
    public IReadOnlyList<Guid> GetActiveDepartmentIds(Guid userId)
    {
        if (!Users.Any(user => user.Id == userId && user.IsActive)) return [];
        var units = Units.ToDictionary(unit => unit.Id);
        return Memberships.Where(member => member.UserId == userId && member.IsActive)
            .Select(member => units[member.UnitId])
            .Where(unit => unit.IsActive)
            .Select(unit => unit.IsDepartment ? unit.Id : unit.ParentId!.Value)
            .Distinct().Order().ToArray();
    }
}

public enum ProjectionDisposition { Prepared, Unchanged, Rejected }
public sealed record ProjectionPreparation(ProjectionDisposition Disposition,
    PreparedOrganizationDirectory? Projection, IReadOnlyList<OrganizationValidationError> Errors);
