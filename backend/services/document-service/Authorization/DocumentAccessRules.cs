namespace DocumentService;

public static class DocumentAccessRules
{
    public static bool CanCreateIncoming(DocumentActor actor) =>
        actor.IsInRole("Admin") || actor.IsInRole("SecretaryDirector") || actor.IsInRole("System");

    public static bool CanCreateDepartmentDocument(DocumentActor actor) =>
        actor.IsInRole("Admin") || actor.IsInRole("SecretaryDept");

    public static bool CanRead(DocumentActor actor, Document document)
    {
        if (document.Registration is not null) return false;
        if (actor.IsInRole("Admin")) return true;

        if (actor.IsInRole("SecretaryDirector"))
            return document.DocType == DocumentTypeConstants.INCOMING ||
                   document.DocType == DocumentTypeConstants.INTERNAL &&
                   document.Status == DocumentStatusConstants.Distributed;

        if (!actor.DepartmentId.HasValue) return false;
        var departmentId = actor.DepartmentId.Value;

        if (actor.IsInRole("SecretaryDept"))
        {
            return document.DocType switch
            {
                DocumentTypeConstants.INCOMING => document.DepartmentAccesses.Any(x => x.DepartmentId == departmentId),
                DocumentTypeConstants.OUTGOING or DocumentTypeConstants.INTERNAL => document.SenderDepartmentId == departmentId,
                _ => false
            };
        }

        return actor.IsInRole("Staff") &&
               document.DocType == DocumentTypeConstants.INCOMING &&
               document.DepartmentAccesses.Any(x => x.DepartmentId == departmentId);
    }

    public static bool CanEdit(DocumentActor actor, Document document)
    {
        if (document.Registration is not null || document.Status != DocumentStatusConstants.Draft) return false;
        if (actor.IsInRole("Admin")) return true;
        if (actor.IsInRole("SecretaryDirector")) return document.DocType == DocumentTypeConstants.INCOMING;

        return actor.IsInRole("SecretaryDept") && actor.DepartmentId.HasValue &&
               document.SenderDepartmentId == actor.DepartmentId &&
               document.DocType is DocumentTypeConstants.OUTGOING or DocumentTypeConstants.INTERNAL;
    }

    public static bool CanAssignDepartments(DocumentActor actor, Document document) =>
        document.Registration is null && document.DocType == DocumentTypeConstants.INCOMING &&
        (actor.IsInRole("Admin") || actor.IsInRole("SecretaryDirector"));

    public static bool CanChangeStatus(DocumentActor actor, Document document)
    {
        if (document.Registration is not null) return false;
        if (actor.IsInRole("Admin")) return true;
        if (actor.IsInRole("SecretaryDirector")) return document.DocType == DocumentTypeConstants.INCOMING;

        return actor.IsInRole("SecretaryDept") && actor.DepartmentId.HasValue &&
               document.SenderDepartmentId == actor.DepartmentId &&
               document.DocType is DocumentTypeConstants.OUTGOING or DocumentTypeConstants.INTERNAL;
    }
}
