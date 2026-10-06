namespace AuthService.Organization;

public static class OrganizationSnapshotValidator
{
    public static IReadOnlyList<OrganizationValidationError> ValidateFullSnapshot(
        IReadOnlyCollection<OrganizationUnitSnapshot> units,
        IReadOnlyCollection<DepartmentLeadershipSnapshot> leadership)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(leadership);
        var errors = new List<OrganizationValidationError>();
        var byId = new Dictionary<Guid, OrganizationUnitSnapshot>();
        foreach (var unit in units)
        {
            if (unit.Id == Guid.Empty) errors.Add(new("UNIT_ID_REQUIRED", unit.Id));
            if (!byId.TryAdd(unit.Id, unit)) errors.Add(new("DUPLICATE_UNIT_ID", unit.Id));
        }

        var roots = byId.Values.Where(n => n.IsDepartment && n.ParentId is null).ToArray();
        if (roots.Length != 1) errors.Add(new("MANAGEMENT_ROOT_REQUIRED", null));
        var managementId = roots.Length == 1 ? roots[0].Id : (Guid?)null;
        foreach (var unit in byId.Values)
        {
            if (unit.ParentId is null)
            {
                if (!unit.IsDepartment) errors.Add(new("GROUP_PARENT_REQUIRED", unit.Id));
                continue;
            }
            if (unit.ParentId == unit.Id)
            {
                errors.Add(new("SELF_PARENT", unit.Id));
                continue;
            }
            if (!byId.TryGetValue(unit.ParentId.Value, out var parent))
            {
                errors.Add(new("PARENT_NOT_FOUND", unit.Id));
                continue;
            }
            if (unit.IsDepartment && unit.ParentId != managementId)
                errors.Add(new("DEPARTMENT_PARENT_NOT_MANAGEMENT", unit.Id));
            if (!unit.IsDepartment && !parent.IsDepartment)
                errors.Add(new("GROUP_PARENT_NOT_DEPARTMENT", unit.Id));
            if (unit.IsActive && !parent.IsActive)
                errors.Add(new("ACTIVE_UNIT_HAS_INACTIVE_PARENT", unit.Id));
        }

        var leadersByDepartment = leadership.GroupBy(l => l.DepartmentId).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var pair in leadersByDepartment)
        {
            if (!byId.TryGetValue(pair.Key, out var unit) || !unit.IsDepartment)
                errors.Add(new("LEADERSHIP_TARGET_NOT_DEPARTMENT", pair.Key));
            if (pair.Value.Length > 1)
                errors.Add(new("DUPLICATE_DEPARTMENT_LEADERSHIP", pair.Key));
            foreach (var assignment in pair.Value)
            {
                if (assignment.LineManagerUserId == Guid.Empty)
                    errors.Add(new("LINE_MANAGER_REQUIRED", pair.Key));
                if (assignment.DeputyLineManagerUserIds is null)
                {
                    errors.Add(new("DEPUTY_LIST_REQUIRED", pair.Key));
                    continue;
                }
                if (assignment.DeputyLineManagerUserIds.Any(id => id == Guid.Empty))
                    errors.Add(new("DEPUTY_USER_ID_REQUIRED", pair.Key));
                if (assignment.DeputyLineManagerUserIds.Distinct().Count() != assignment.DeputyLineManagerUserIds.Count)
                    errors.Add(new("DUPLICATE_DEPUTY", pair.Key));
            }
        }
        foreach (var unit in byId.Values.Where(n => n.IsDepartment && n.IsActive))
        {
            if (!leadersByDepartment.ContainsKey(unit.Id))
                errors.Add(new("LINE_MANAGER_REQUIRED", unit.Id));
        }
        return errors;
    }
}
