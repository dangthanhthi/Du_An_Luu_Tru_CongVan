namespace DocumentService;

internal static class V2EditAuthority
{
    public static bool CanEdit(V2EditorActor actor, DocumentRegistration header) =>
        IsParticipant(actor, header) || actor.LineManagerDepartmentIds.Contains(header.OwnerDepartmentId) ||
        actor.DeputyManagerDepartmentIds.Contains(header.OwnerDepartmentId);

    public static bool IsParticipant(V2EditorActor actor, DocumentRegistration header) =>
        actor.UserId == header.InputterUserId || actor.UserId == header.OriginatorUserId;
}
