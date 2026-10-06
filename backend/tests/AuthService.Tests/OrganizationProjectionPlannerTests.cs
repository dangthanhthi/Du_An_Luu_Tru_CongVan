using AuthService.Organization;
using Xunit;

namespace AuthService.Tests;

public sealed class OrganizationProjectionPlannerTests
{
    private static readonly Guid Management = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Finance = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Group = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Gm = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Lm = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid Deputy = Guid.Parse("00000000-0000-0000-0000-000000000012");
    private static readonly Guid Staff = Guid.Parse("00000000-0000-0000-0000-000000000013");

    [Fact]
    public void Complete_snapshot_preserves_external_ids_hierarchy_and_multiple_deputies()
    {
        var result = OrganizationProjectionPlanner.Prepare(null, Snapshot());
        Assert.Equal(ProjectionDisposition.Prepared, result.Disposition);
        Assert.Empty(result.Errors);
        var projection = Assert.IsType<PreparedOrganizationDirectory>(result.Projection);
        Assert.Equal(Management, projection.Units.Single(unit => unit.ParentId is null).Id);
        Assert.Equal(Finance, projection.Units.Single(unit => !unit.IsDepartment).ParentId);
        Assert.Equal(new[] { Deputy, Staff }, projection.Leadership.Single(unit => unit.DepartmentId == Finance).DeputyLineManagerUserIds);
    }

    [Fact]
    public void Group_membership_resolves_to_its_department_without_promoting_the_group()
    {
        var projection = Accepted(Snapshot());
        Assert.Equal(new[] { Finance }, projection.GetActiveDepartmentIds(Staff));
        Assert.Empty(projection.GetActiveDepartmentIds(Guid.NewGuid()));
    }

    [Fact]
    public void Inactive_user_or_membership_cannot_resolve_an_active_department()
    {
        var source = Snapshot();
        var inactiveUser = source with { Users = source.Users.Select(user => user.Id == Staff ? user with { IsActive = false } : user).ToArray(),
            Leadership = [new(Management, Gm, []), new(Finance, Lm, [Deputy])] };
        Assert.Empty(Accepted(inactiveUser).GetActiveDepartmentIds(Staff));
        var inactiveMembership = source with { Memberships = [new(Group, Staff, false)] };
        Assert.Empty(Accepted(inactiveMembership).GetActiveDepartmentIds(Staff));
    }

    [Fact]
    public void Older_snapshot_does_not_restore_revoked_user_or_roll_back_directory()
    {
        var source = Snapshot();
        var newer = source with { Sequence = 9,
            Users = source.Users.Select(user => user.Id == Staff ? user with { IsActive = false } : user).ToArray(),
            Leadership = [new(Management, Gm, []), new(Finance, Lm, [Deputy])] };
        var current = Accepted(newer);
        var result = OrganizationProjectionPlanner.Prepare(current, source);
        Assert.Equal(ProjectionDisposition.Unchanged, result.Disposition);
        Assert.Same(current, result.Projection);
        Assert.Empty(result.Projection!.GetActiveDepartmentIds(Staff));
    }

    [Fact]
    public void Reordered_identical_snapshot_is_an_idempotent_replay()
    {
        var source = Snapshot();
        var current = Accepted(source);
        var reordered = source with { Units = source.Units.Reverse().ToArray(), Users = source.Users.Reverse().ToArray(),
            Leadership = source.Leadership.Reverse().Select(item => item with { DeputyLineManagerUserIds = item.DeputyLineManagerUserIds.Reverse().ToArray() }).ToArray() };
        var result = OrganizationProjectionPlanner.Prepare(current, reordered);
        Assert.Equal(ProjectionDisposition.Unchanged, result.Disposition);
        Assert.Same(current, result.Projection);
    }

    [Fact]
    public void Same_sequence_with_different_content_is_rejected_and_keeps_previous_snapshot()
    {
        var source = Snapshot();
        var current = Accepted(source);
        var changed = source with { Units = source.Units.Select(unit => unit.Id == Finance ? unit with { Name = "Changed name" } : unit).ToArray() };
        var result = OrganizationProjectionPlanner.Prepare(current, changed);
        Assert.Equal(ProjectionDisposition.Rejected, result.Disposition);
        Assert.Contains(result.Errors, error => error.Code == "SNAPSHOT_SEQUENCE_CONFLICT");
        Assert.Same(current, result.Projection);
    }

    [Fact]
    public void Invalid_newer_hierarchy_cannot_partially_replace_the_current_snapshot()
    {
        var source = Snapshot();
        var current = Accepted(source);
        var invalid = source with { Sequence = 2, Units = source.Units.Select(unit => unit.Id == Group ? unit with { ParentId = Guid.NewGuid() } : unit).ToArray() };
        var result = OrganizationProjectionPlanner.Prepare(current, invalid);
        Assert.Equal(ProjectionDisposition.Rejected, result.Disposition);
        Assert.Contains(result.Errors, error => error.Code == "PARENT_NOT_FOUND");
        Assert.Same(current, result.Projection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_or_inactive_leadership_identity_is_rejected(bool missing)
    {
        var source = Snapshot();
        var invalid = source with { Users = source.Users.Where(user => !missing || user.Id != Deputy)
            .Select(user => user.Id == Deputy ? user with { IsActive = false } : user).ToArray() };
        var result = OrganizationProjectionPlanner.Prepare(null, invalid);
        Assert.Equal(ProjectionDisposition.Rejected, result.Disposition);
        Assert.Contains(result.Errors, error => error.Code == "LEADERSHIP_USER_NOT_ACTIVE");
    }

    [Fact]
    public void Membership_cannot_reference_an_unknown_unit_or_user()
    {
        var result = OrganizationProjectionPlanner.Prepare(null, Snapshot() with { Memberships = [new(Guid.NewGuid(), Guid.NewGuid(), true)] });
        Assert.Equal(ProjectionDisposition.Rejected, result.Disposition);
        Assert.Contains(result.Errors, error => error.Code == "MEMBERSHIP_UNIT_NOT_FOUND");
        Assert.Contains(result.Errors, error => error.Code == "MEMBERSHIP_USER_NOT_FOUND");
    }

    [Fact]
    public void Mutable_input_lists_cannot_change_an_accepted_projection()
    {
        var source = Snapshot();
        var mutableUnits = source.Units.ToList();
        var mutableDeputies = new List<Guid> { Deputy, Staff };
        var projection = Accepted(source with { Units = mutableUnits, Leadership = [new(Management, Gm, []), new(Finance, Lm, mutableDeputies)] });
        mutableUnits.Clear();
        mutableDeputies.Clear();
        Assert.Equal(3, projection.Units.Length);
        Assert.Equal(2, projection.Leadership.Single(item => item.DepartmentId == Finance).DeputyLineManagerUserIds.Count);
    }

    [Fact]
    public void Unknown_schema_order_or_duplicate_identity_is_rejected()
    {
        var source = Snapshot();
        Assert.Contains(OrganizationProjectionPlanner.Prepare(null, source with { Sequence = 0 }).Errors,
            error => error.Code == "SNAPSHOT_SEQUENCE_REQUIRED");
        Assert.Contains(OrganizationProjectionPlanner.Prepare(null, source with { Users = [.. source.Users, source.Users[0]] }).Errors,
            error => error.Code == "DUPLICATE_USER_ID");
    }

    private static PreparedOrganizationDirectory Accepted(OrganizationDirectorySnapshot snapshot)
    {
        var result = OrganizationProjectionPlanner.Prepare(null, snapshot);
        Assert.Empty(result.Errors);
        return Assert.IsType<PreparedOrganizationDirectory>(result.Projection);
    }

    private static OrganizationDirectorySnapshot Snapshot() => new(1,
        [new(Management, "Management", "MGT", null, true, true), new(Finance, "Finance", "FIN", Management, true, true), new(Group, "Accounts", null, Finance, false, true)],
        [new(Gm, "General Manager", true), new(Lm, "Line Manager", true), new(Deputy, "Deputy", true), new(Staff, "Staff", true)],
        [new(Group, Staff, true)], [new(Management, Gm, []), new(Finance, Lm, [Deputy, Staff])]);
}
