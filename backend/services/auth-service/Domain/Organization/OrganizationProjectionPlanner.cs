using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuthService.Organization;

public static class OrganizationProjectionPlanner
{
    public static ProjectionPreparation Prepare(PreparedOrganizationDirectory? current, OrganizationDirectorySnapshot incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (current is not null && incoming.Sequence > 0 && incoming.Sequence < current.Sequence)
            return new(ProjectionDisposition.Unchanged, current, []);

        var errors = new List<OrganizationValidationError>();
        if (incoming.Sequence <= 0) errors.Add(new("SNAPSHOT_SEQUENCE_REQUIRED", null));
        if (incoming.Units is null || incoming.Users is null || incoming.Memberships is null || incoming.Leadership is null)
            return new(ProjectionDisposition.Rejected, current, [new("SNAPSHOT_LIST_REQUIRED", null)]);

        errors.AddRange(OrganizationSnapshotValidator.ValidateFullSnapshot(
            incoming.Units.Select(unit => new OrganizationUnitSnapshot(unit.Id, unit.ParentId, unit.IsDepartment, unit.IsActive)).ToArray(),
            incoming.Leadership));
        var unitsById = incoming.Units.DistinctBy(unit => unit.Id).ToDictionary(unit => unit.Id);
        var usersById = new Dictionary<Guid, DirectoryUser>();
        foreach (var user in incoming.Users)
        {
            if (user.Id == Guid.Empty) errors.Add(new("USER_ID_REQUIRED", user.Id));
            if (string.IsNullOrWhiteSpace(user.DisplayName)) errors.Add(new("USER_DISPLAY_NAME_REQUIRED", user.Id));
            if (!usersById.TryAdd(user.Id, user)) errors.Add(new("DUPLICATE_USER_ID", user.Id));
        }
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in incoming.Units)
        {
            if (string.IsNullOrWhiteSpace(unit.Name)) errors.Add(new("UNIT_NAME_REQUIRED", unit.Id));
            if (unit.IsDepartment && string.IsNullOrWhiteSpace(unit.Code)) errors.Add(new("DEPARTMENT_CODE_REQUIRED", unit.Id));
            if (!string.IsNullOrWhiteSpace(unit.Code) && !codes.Add(unit.Code.Trim())) errors.Add(new("DUPLICATE_UNIT_CODE", unit.Id));
        }
        var memberships = new HashSet<(Guid UnitId, Guid UserId)>();
        foreach (var membership in incoming.Memberships)
        {
            if (!unitsById.ContainsKey(membership.UnitId)) errors.Add(new("MEMBERSHIP_UNIT_NOT_FOUND", membership.UnitId));
            if (!usersById.ContainsKey(membership.UserId)) errors.Add(new("MEMBERSHIP_USER_NOT_FOUND", membership.UnitId));
            if (!memberships.Add((membership.UnitId, membership.UserId))) errors.Add(new("DUPLICATE_MEMBERSHIP", membership.UnitId));
        }
        foreach (var leadership in incoming.Leadership)
        {
            var leaders = new[] { leadership.LineManagerUserId }.Concat(leadership.DeputyLineManagerUserIds ?? []);
            foreach (var userId in leaders)
            {
                if (!usersById.TryGetValue(userId, out var user) || !user.IsActive)
                    errors.Add(new("LEADERSHIP_USER_NOT_ACTIVE", leadership.DepartmentId));
            }
        }
        if (errors.Count > 0) return new(ProjectionDisposition.Rejected, current, errors.ToImmutableArray());

        // Detached immutable values prevent caller mutations from changing a prepared
        // snapshot after validation. Canonical ordering makes redelivery deterministic.
        var units = incoming.Units.OrderBy(unit => unit.Id).ToImmutableArray();
        var users = incoming.Users.OrderBy(user => user.Id).ToImmutableArray();
        var memberValues = incoming.Memberships.OrderBy(member => member.UnitId).ThenBy(member => member.UserId).ToImmutableArray();
        var leaderValues = incoming.Leadership.OrderBy(leader => leader.DepartmentId)
            .Select(leader => leader with { DeputyLineManagerUserIds = leader.DeputyLineManagerUserIds.Order().ToImmutableArray() }).ToImmutableArray();
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new { Units = units, Users = users, Memberships = memberValues, Leadership = leaderValues });
        var fingerprint = Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
        if (current is not null && incoming.Sequence == current.Sequence)
        {
            return fingerprint == current.Fingerprint
                ? new(ProjectionDisposition.Unchanged, current, [])
                : new(ProjectionDisposition.Rejected, current, [new("SNAPSHOT_SEQUENCE_CONFLICT", null)]);
        }
        var prepared = new PreparedOrganizationDirectory(incoming.Sequence, fingerprint, units, users, memberValues, leaderValues);
        return new(ProjectionDisposition.Prepared, prepared, []);
    }
}
