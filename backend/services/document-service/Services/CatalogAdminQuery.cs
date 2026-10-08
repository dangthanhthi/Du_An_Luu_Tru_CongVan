using System.Globalization;

namespace DocumentService;

public sealed record CatalogAdminQuery(string Group, string Activity, string? SearchTerm, int PageNumber, int PageSize)
{
    public static CatalogAdminQuery Parse(IQueryCollection query)
    {
        string[] allowed = ["group", "activity", "searchTerm", "pageNumber", "pageSize"];
        if (query.Any(x => !allowed.Contains(x.Key, StringComparer.Ordinal) || x.Value.Count != 1 || string.IsNullOrWhiteSpace(x.Value[0])))
            throw Invalid();
        var group = query["group"].ToString();
        if (!CatalogService.IsGroup(group)) throw Invalid();
        var activity = query.ContainsKey("activity") ? query["activity"].ToString() : "Active";
        if (activity is not ("Active" or "Inactive" or "All")) throw Invalid();
        var search = query.ContainsKey("searchTerm") ? query["searchTerm"].ToString().Trim() : null;
        if (search is not null && search.Length is < 1 or > 200) throw Invalid();
        return new(group, activity, search, Number(query, "pageNumber", 1, 1000000), Number(query, "pageSize", 20, 100));
    }

    private static int Number(IQueryCollection query, string key, int fallback, int maximum)
    {
        if (!query.ContainsKey(key)) return fallback;
        var value = query[key].ToString();
        if (value.Any(c => c is < '0' or > '9') || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 || number > maximum)
            throw Invalid();
        return number;
    }
    private static CatalogRuleException Invalid() => new(400, "INVALID_ADMIN_CATALOG_QUERY", "Admin catalog query is invalid.");
}

public sealed record CatalogAdminPage(string Group, string Activity, IReadOnlyList<CatalogItemDto> Items,
    int TotalCount, int PageNumber, int PageSize, bool CanEditGroup);
