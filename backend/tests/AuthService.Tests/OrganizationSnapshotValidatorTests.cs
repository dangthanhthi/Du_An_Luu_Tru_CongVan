using AuthService.Organization;
using Xunit;

namespace AuthService.Tests;

public sealed class OrganizationSnapshotValidatorTests
{
    private static readonly Guid Management = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Finance = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Group = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Manager = Guid.Parse("00000000-0000-0000-0000-000000000004");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Full_snapshot_accepts_management_department_groups_and_multiple_deputies(int count)
    {
        var deputies = Enumerable.Range(10, count)
            .Select(n => Guid.Parse($"00000000-0000-0000-0000-{n:D12}")).ToArray();
        var leaders = Leaders() with
        {
            DeputyLineManagerUserIds = deputies
        };

        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot(Nodes(), [RootLeader(), leaders]);

        Assert.Empty(errors);
    }

    [Fact]
    public void A_group_cannot_be_the_management_root()
    {
        var nodes = Nodes().Select(n => n.Id == Management ? n with { IsDepartment = false } : n).ToArray();
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot(nodes, [RootLeader(), Leaders()]);
        Assert.Contains(errors, e => e.Code == "MANAGEMENT_ROOT_REQUIRED");
        Assert.Contains(errors, e => e.Code == "GROUP_PARENT_REQUIRED");
    }

    [Fact]
    public void Group_must_belong_to_a_department_instead_of_another_group()
    {
        var subgroup = new OrganizationUnitSnapshot(Guid.NewGuid(), Group, false, true);
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot([.. Nodes(), subgroup], [RootLeader(), Leaders()]);
        Assert.Contains(errors, e => e.Code == "GROUP_PARENT_NOT_DEPARTMENT" && e.UnitId == subgroup.Id);
    }

    [Fact]
    public void Child_department_cannot_point_to_a_peer_department()
    {
        var peer = new OrganizationUnitSnapshot(Guid.NewGuid(), Finance, true, true);
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot([.. Nodes(), peer],
            [RootLeader(), Leaders(), new(peer.Id, Manager, [])]);
        Assert.Contains(errors, e => e.Code == "DEPARTMENT_PARENT_NOT_MANAGEMENT" && e.UnitId == peer.Id);
    }

    [Fact]
    public void An_unknown_parent_does_not_become_an_implicit_root()
    {
        var nodes = Nodes().Select(n => n.Id == Finance ? n with { ParentId = Guid.NewGuid() } : n).ToArray();
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot(nodes, [RootLeader(), Leaders()]);
        Assert.Contains(errors, e => e.Code == "PARENT_NOT_FOUND" && e.UnitId == Finance);
    }

    [Fact]
    public void Every_active_department_needs_one_line_manager()
    {
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot(Nodes(), [RootLeader()]);
        Assert.Contains(errors, e => e.Code == "LINE_MANAGER_REQUIRED" && e.UnitId == Finance);
    }

    [Fact]
    public void Multiple_leadership_records_for_the_same_department_are_rejected()
    {
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot(Nodes(),
            [RootLeader(), Leaders(), new(Finance, Guid.NewGuid(), [])]);
        Assert.Contains(errors, e => e.Code == "DUPLICATE_DEPARTMENT_LEADERSHIP" && e.UnitId == Finance);
    }

    [Fact]
    public void Duplicate_ids_and_self_parent_are_rejected_without_throwing_a_dictionary_error()
    {
        var nodes = Nodes().Select(n => n.Id == Group ? n with { ParentId = Group } : n).ToArray();
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot([.. nodes, nodes[1]], [RootLeader(), Leaders()]);
        Assert.Contains(errors, e => e.Code == "DUPLICATE_UNIT_ID");
        Assert.Contains(errors, e => e.Code == "SELF_PARENT");
    }

    private static OrganizationUnitSnapshot[] Nodes() =>
    [
        new(Management, null, true, true),
        new(Finance, Management, true, true),
        new(Group, Finance, false, true)
    ];

    [Fact]
    public void Missing_deputy_list_is_a_validation_error_instead_of_a_null_reference()
    {
        var malformed = new DepartmentLeadershipSnapshot(Finance, Manager, null!);
        var errors = OrganizationSnapshotValidator.ValidateFullSnapshot(Nodes(), [RootLeader(), malformed]);
        Assert.Contains(errors, e => e.Code == "DEPUTY_LIST_REQUIRED" && e.UnitId == Finance);
    }

    private static DepartmentLeadershipSnapshot Leaders() => new(Finance, Manager, []);
    private static DepartmentLeadershipSnapshot RootLeader() => new(Management,
        Guid.Parse("00000000-0000-0000-0000-000000000005"), []);
}
