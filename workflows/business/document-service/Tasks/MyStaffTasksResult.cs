namespace DocumentService;

// UTC DateTime serializes with Z as required by the J21 wire contract.
public sealed record MyStaffTaskItem(string TaskId,StaffMember Assignee,string Title,string Status,DateTime? DueAt);
public sealed record MyStaffTasksPage(IReadOnlyList<MyStaffTaskItem> Items,int Total,int PageNumber,int PageSize);
public sealed record MyStaffTasksResult(string TaskState,StaffMember? SelectedAssignee,MyStaffTasksPage? Tasks);
