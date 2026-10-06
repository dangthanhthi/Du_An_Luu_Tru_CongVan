namespace DocumentService;

public static class BusinessCatalogSeed
{
    public static IReadOnlyList<BusinessCatalogEntry> Entries()
    {
        var entries = new List<BusinessCatalogEntry>();
        Add("companies", 1, [("HL","Hoàng Long"),("HV","Hoàn Vũ"),("HLHV","Hoàng Long Hoàn Vũ")]);
        Add("methods", 2, [("FAX","Fax"),("COURIER","Courier"),("EMAIL","eMail"),("HAND_DELIVER","Pick-up/Hand-Deliver"),("EMAIL_FAX","Email & Fax")]);
        Add("documentTypes", 3, [("LETTER","Letter"),("NOTIFICATION","Notification"),("ANNOUNCEMENT","Announcement"),
            ("APPROVAL_REQUEST","Approval / Request"),("INVITATION","Invitation"),("STATEMENT","Statement")]);
        Add("internalTypes", 4, [("MEMO","Inter-Office Memo"),("REPORT","Report"),("STATEMENT","Statement"),("PURCHASE_REQUEST","Purchase Request"),("OTHERS","Others")]);
        Add("sensitivity", 5, [("Normal","Normal"),("Confidential","Confidential")]);
        return entries;
        void Add(string group, int prefix, (string Code,string Name)[] values)
        {
            for (var i = 0; i < values.Length; i++) entries.Add(new()
            { Id = StableId(prefix, i + 1), Group = group, Code = values[i].Code, Name = values[i].Name, SortOrder = i + 1 });
        }
    }
    public static IReadOnlyList<DistributionTarget> Targets()
    {
        (string Name,string? Initial)[] values = [("Management","MGM"),("Production","PRD"),("HSE","HSE"),("Project","PRJ"),
            ("Subsurface","SUB"),("Finance","FIN"),("Administration","ADM"),("C&P","C&P"),("Drilling","DRI"),
            ("HLHV Partners",null),("Secretary List",null),("VT Shore Base",null),("HLHV Members",null),("HLHV Managers","HLHVM")];
        return values.Select((x,i) => new DistributionTarget
        { Id = StableId(6, i + 1), LegacyId = i + 1, Name = x.Name, Initial = x.Initial }).ToArray();
    }
    private static Guid StableId(int group, int index) => Guid.Parse($"10000000-0000-4000-8000-{group:D4}{index:D8}");
}

