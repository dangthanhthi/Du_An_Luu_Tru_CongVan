using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
namespace DocumentService;
public sealed record CatalogItemDto(Guid Id, string Group, string Code, string Name, int SortOrder, bool IsActive, long Version);
public sealed record CatalogUpdateRequest(string Name, int SortOrder, bool IsActive, long Version);
public sealed class CatalogRuleException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }

public sealed class CatalogService(DocumentDbContext db, TimeProvider clock)
{
    private static readonly string[] Groups = ["companies", "methods", "documentTypes", "internalTypes", "sensitivity", "categories"];
    private static readonly string[] EditableGroups = ["methods", "documentTypes", "internalTypes", "categories"];

    public static bool IsGroup(string group) => Groups.Contains(group);

    public async Task<CatalogAdminPage> GetAdminPageAsync(CatalogAdminQuery request, Guid actor, CancellationToken ct = default)
    {
        ValidateActor(actor);
        // Serializable gives count and page one consistent read without requiring database-wide RCSI.
        // SQL Server's configured retry strategy must own the entire transaction.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var query = db.BusinessCatalogEntries.AsNoTracking().Where(x => x.Group == request.Group);
            if (request.Activity != "All") query = query.Where(x => x.IsActive == (request.Activity == "Active"));
            if (request.SearchTerm is not null)
            {
                var pattern = "%" + request.SearchTerm.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[").Replace("]", "\\]") + "%";
                query = query.Where(x => EF.Functions.Like(x.Name, pattern, "\\") || EF.Functions.Like(x.Code, pattern, "\\"));
            }
            var total = await query.CountAsync(ct);
            var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Code).ThenBy(x => x.Id)
                .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
            foreach (var row in rows) ValidateWireData(row, request.Group);
            var page = new CatalogAdminPage(request.Group, request.Activity, rows.Select(ToDto).ToArray(), total,
                request.PageNumber, request.PageSize, EditableGroups.Contains(request.Group));
            await transaction.CommitAsync(ct);
            return page;
        });
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<CatalogItemDto>>> GetAsync(string? groups, CancellationToken ct = default)
    {
        if (groups?.Length > 256) throw Rule(400, "INVALID_GROUP", "Catalog groups are invalid.");
        var requested = groups is null ? Groups : groups.Split(',').Select(x => x.Trim()).Distinct().ToArray();
        if (requested.Length == 0 || requested.Any(x => !Groups.Contains(x)))
            throw Rule(400, "INVALID_GROUP", "Catalog group is not recognized.");
        var rows = await db.BusinessCatalogEntries.AsNoTracking().Where(x => x.IsActive && requested.Contains(x.Group))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code).ToListAsync(ct);
        return requested.ToDictionary(x => x, x => (IReadOnlyList<CatalogItemDto>)rows.Where(r => r.Group == x).Select(ToDto).ToArray());
    }

    public async Task<CatalogItemDto> CreateAsync(string group, string code, string name, Guid actor, CancellationToken ct = default)
    {
        ValidateActor(actor);
        if (!EditableGroups.Contains(group)) throw Rule(400, "FIXED_OR_UNKNOWN_GROUP", "This catalog is fixed or not recognized.");
        code = (code ?? "").Trim().ToUpperInvariant();
        ValidateName(name);
        if (code.Length is < 1 or > 64 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw Rule(400, "INVALID_CODE", "Code must contain only letters, digits and underscore.");
        if (await db.BusinessCatalogEntries.AnyAsync(x => x.Group == group && x.Code == code, ct))
            throw Rule(409, "DUPLICATE_CODE", "Catalog code already exists.");
        var entry = new BusinessCatalogEntry { Group = group, Code = code, Name = name.Trim() };
        db.BusinessCatalogEntries.Add(entry);
        Audit(entry, actor, "Created", "");
        await SaveAsync(ct);
        return ToDto(entry);
    }

    public async Task<CatalogItemDto> UpdateAsync(Guid id, CatalogUpdateRequest request, Guid actor, CancellationToken ct = default)
    {
        ValidateActor(actor);
        ValidateName(request.Name);
        if (request.SortOrder is < 0 or > 10000 || request.Version < 1)
            throw Rule(400, "INVALID_CATALOG_UPDATE", "Catalog version or order is invalid.");
        var entry = await db.BusinessCatalogEntries.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Rule(404, "CATALOG_NOT_FOUND", "Catalog entry not found.");
        if (!EditableGroups.Contains(entry.Group))
            throw Rule(400, "FIXED_GROUP", "Company and sensitivity codes are fixed in this release.");
        if (entry.Version != request.Version) throw Rule(409, "VERSION_CONFLICT", "Catalog entry has changed. Reload it before saving.");
        var before = JsonSerializer.Serialize(ToDto(entry));
        entry.Name = request.Name.Trim();
        entry.SortOrder = request.SortOrder;
        entry.IsActive = request.IsActive;
        entry.Version = checked(entry.Version + 1);
        Audit(entry, actor, "Updated", before);
        await SaveAsync(ct);
        return ToDto(entry);
    }

    public static CatalogItemDto ToDto(BusinessCatalogEntry x) => new(x.Id, x.Group, x.Code, x.Name, x.SortOrder, x.IsActive, x.Version);
    private static void ValidateWireData(BusinessCatalogEntry row, string group)
    {
        if (row.Id == Guid.Empty || row.Group != group || !IsGroup(row.Group) || string.IsNullOrEmpty(row.Code) || row.Code.Length > 64 ||
            row.Code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') || string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 200 ||
            row.SortOrder is < 0 or > 10000 || row.Version is < 1 or > 9007199254740991)
            throw Rule(503, "CATALOG_DATA_INVALID", "Stored catalog data is invalid.");
    }
    private void Audit(BusinessCatalogEntry entry, Guid actor, string action, string before) =>
        db.CatalogAuditEvents.Add(new()
        { EntryId = entry.Id, ActorUserId = actor, Action = action, BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(ToDto(entry)), CreatedAt = clock.GetUtcNow() });
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); } // Entry and audit share EF's relational transaction.
        catch (DbUpdateConcurrencyException)
        { db.ChangeTracker.Clear(); throw Rule(409, "VERSION_CONFLICT", "Catalog entry has changed. Reload it before saving."); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 } ||
            e.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        { db.ChangeTracker.Clear(); throw Rule(409, "DUPLICATE_CODE", "Catalog code already exists."); }
    }
    private static void ValidateActor(Guid actor)
    { if (actor == Guid.Empty) throw Rule(401, "ACTOR_REQUIRED", "Authentication is required."); }
    private static void ValidateName(string? name)
    { if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw Rule(400, "INVALID_NAME", "A name of at most 200 characters is required."); }
    private static CatalogRuleException Rule(int status, string code, string message) => new(status, code, message);
}

