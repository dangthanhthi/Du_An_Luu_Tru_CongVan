using System.Net.Mail;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace PartnerService;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreatePartnerRequest(string FullName, string? ShortName = null, string EntityType = "Both",
    string? Email = null, string? Phone = null, string? Address = null, string? TaxCode = null,
    string? ContactPerson = null, string? ContactInformation = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdatePartnerRequest(string FullName, long ExpectedVersion, string? ShortName = null, string EntityType = "Both",
    string? Email = null, string? Phone = null, string? Address = null, string? TaxCode = null,
    string? ContactPerson = null, string? ContactInformation = null, bool IsActive = true);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record PartnerVersionRequest(long ExpectedVersion);
public record PartnerFilter(string? SearchTerm, string? EntityType, bool? IsActive, int PageNumber = 1, int PageSize = 10, bool IncludeDeleted = false);
public record PartnerView(Guid Id, string FullName, string? ShortName, string EntityType, string? Email, string? Phone,
    string? Address, string? TaxCode, string? ContactPerson, string? ContactInformation, bool IsActive, bool IsDeleted, long Version);
public record PagedResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize)
{ public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize); }
public sealed class PartnerRuleException(int status, string code, string message) : Exception(message)
{ public int Status { get; } = status; public string Code { get; } = code; }

public interface IPartnerBusinessService
{
    Task<PartnerView> CreateAsync(CreatePartnerRequest req, Guid actor, CancellationToken ct = default);
    Task<PartnerView> UpdateAsync(Guid id, UpdatePartnerRequest req, Guid actor, CancellationToken ct = default);
    Task<PartnerView?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PartnerView>> GetListAsync(PartnerFilter filter, CancellationToken ct = default);
    Task<PartnerView> ChangeDeletionAsync(Guid id, bool deleted, long version, Guid actor, CancellationToken ct = default);
}

// Callers enforce CatalogManage from the trusted issuer, never from browser roles.
public sealed class PartnerBusinessService(PartnerDbContext db) : IPartnerBusinessService
{
    private static PartnerRuleException Invalid(string message) => new(400, "PARTNER_INVALID", message);
    private static PartnerRuleException Conflict() => new(409, "PARTNER_VERSION_CONFLICT", "Đơn vị đã thay đổi. Hãy tải lại trước khi lưu.");
    private static string? Text(string? value, int limit, string field, bool multiline = false)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (text?.Length > limit || text?.Any(c => char.IsControl(c) && !(multiline && "\r\n\t".Contains(c))) == true) throw Invalid($"{field} không hợp lệ hoặc vượt giới hạn {limit} ký tự.");
        return text;
    }
    private static void Actor(Guid actor)
    { if (actor == Guid.Empty) throw new PartnerRuleException(403, "PARTNER_ACTOR_REQUIRED", "Không xác định được người thực hiện."); }
    private static void Apply(Partner p, CreatePartnerRequest r)
    {
        p.FullName = Text(r.FullName, 500, "Tên đầy đủ") ?? throw Invalid("Tên đầy đủ là bắt buộc.");
        p.ShortName = Text(r.ShortName, 450, "Tên viết tắt");p.TaxCode = Text(r.TaxCode, 450, "Mã số thuế");
        p.NormalizedShortName = p.ShortName?.ToUpperInvariant();p.NormalizedTaxCode = p.TaxCode?.ToUpperInvariant();
        if (!PartnerEntityTypeConstants.AllValues.Contains(r.EntityType)) throw Invalid("Loại đơn vị phải là Sender, Recipient hoặc Both.");
        p.EntityType = r.EntityType;p.Email = Text(r.Email, 254, "Email");
        if (p.Email is { } email && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)) throw Invalid("Email không hợp lệ.");
        p.Phone = Text(r.Phone, 50, "Điện thoại");
        if (p.Phone is { } phone && (phone.Count(char.IsDigit) < 3 || phone.Any(c => !char.IsDigit(c) && !"+()- .xX#".Contains(c)))) throw Invalid("Điện thoại không hợp lệ.");
        p.Address = Text(r.Address, 1000, "Địa chỉ", true);p.ContactPerson = Text(r.ContactPerson, 500, "Người liên hệ");p.ContactInformation = Text(r.ContactInformation, 2000, "Thông tin liên hệ", true);
    }
    public async Task<PartnerView> CreateAsync(CreatePartnerRequest req, Guid actor, CancellationToken ct = default)
    {
        Actor(actor);var p = new Partner { CreatedByUserId = actor, CreatedAt = DateTime.UtcNow };
        Apply(p, req);db.Partners.Add(p);await Save(p, actor, "Create", ct);return View(p);
    }
    public async Task<PartnerView> UpdateAsync(Guid id, UpdatePartnerRequest req, Guid actor, CancellationToken ct = default)
    {
        Actor(actor);var p = await Current(id, req.ExpectedVersion, ct);
        if (p.IsDeleted) throw new PartnerRuleException(409, "PARTNER_DELETED", "Khôi phục đơn vị trước khi sửa.");
        Apply(p, new(req.FullName,req.ShortName,req.EntityType,req.Email,req.Phone,req.Address,req.TaxCode,req.ContactPerson,req.ContactInformation));
        p.IsActive = req.IsActive;p.Version++;await Save(p, actor, "Update", ct);return View(p);
    }
    public async Task<PartnerView> ChangeDeletionAsync(Guid id, bool deleted, long version, Guid actor, CancellationToken ct = default)
    {
        Actor(actor);var p = await Current(id, version, ct);
        if (p.IsDeleted == deleted) throw new PartnerRuleException(409, "PARTNER_STATE_CONFLICT", "Trạng thái đơn vị đã thay đổi.");
        // Preserve the previous active flag: restoring an inactive entry stays inactive.
        p.IsDeleted = deleted;p.DeletedAt = deleted ? DateTime.UtcNow : null;p.Version++;
        await Save(p, actor, deleted ? "Delete" : "Restore", ct);return View(p);
    }
    private async Task<Partner> Current(Guid id, long version, CancellationToken ct)
    {
        if (version < 1 || version >= 9007199254740991L) throw Invalid("Phiên bản không hợp lệ.");
        var p = await db.Partners.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new PartnerRuleException(404, "PARTNER_NOT_FOUND", "Không tìm thấy đơn vị.");
        if (p.Version != version) throw Conflict();return p;
    }
    private async Task Save(Partner p, Guid actor, string action, CancellationToken ct)
    {
        p.UpdatedAt = action == "Create" ? null : DateTime.UtcNow;
        db.PartnerAudits.Add(new() { PartnerId=p.Id, ActorUserId=actor, Action=action, Version=p.Version, CreatedAt=DateTime.UtcNow });
        try { await db.SaveChangesAsync(ct); } // Entity and audit share one relational transaction.
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear();throw Conflict(); }
        catch (DbUpdateException e) when (Duplicate(e))
        { db.ChangeTracker.Clear();throw new PartnerRuleException(409,"PARTNER_DUPLICATE","Tên viết tắt hoặc mã số thuế đã được sử dụng, kể cả bởi đơn vị đã xóa."); }
    }
    private static bool Duplicate(DbUpdateException e) => e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }
        || e.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 };
    public async Task<PartnerView?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var p=await db.Partners.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);return p is null?null:View(p); }
    public async Task<PagedResult<PartnerView>> GetListAsync(PartnerFilter f, CancellationToken ct = default)
    {
        if(f.PageNumber is <1 or >1000000 || f.PageSize is <1 or >100 || f.SearchTerm?.Length>200) throw Invalid("Tham số tra cứu không hợp lệ.");
        if(f.EntityType is not null && !PartnerEntityTypeConstants.AllValues.Contains(f.EntityType)) throw Invalid("Loại đơn vị không hợp lệ.");
        var q=db.Partners.AsNoTracking();if(f.IncludeDeleted)q=q.IgnoreQueryFilters();
        if(!string.IsNullOrWhiteSpace(f.SearchTerm)){var term=f.SearchTerm.Trim().ToUpper();q=q.Where(p=>p.FullName.ToUpper().Contains(term)||(p.ShortName!=null&&p.ShortName.ToUpper().Contains(term)));}
        if(f.EntityType is not null)q=q.Where(p=>p.EntityType==f.EntityType);
        if(f.IsActive is {} active)q=active?q.Where(p=>p.IsActive&&!p.IsDeleted):q.Where(p=>!p.IsActive||p.IsDeleted);
        var count=await q.CountAsync(ct);var rows=await q.OrderBy(p=>p.FullName).ThenBy(p=>p.Id).Skip((f.PageNumber-1)*f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return new(rows.Select(View).ToList(),count,f.PageNumber,f.PageSize);
    }
    public static PartnerView View(Partner p)=>new(p.Id,p.FullName,p.ShortName,p.EntityType,p.Email,p.Phone,p.Address,p.TaxCode,p.ContactPerson,p.ContactInformation,p.IsActive&&!p.IsDeleted,p.IsDeleted,p.Version);
}
