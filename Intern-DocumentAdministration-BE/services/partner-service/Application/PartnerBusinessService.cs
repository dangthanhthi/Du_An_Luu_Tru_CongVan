using Microsoft.EntityFrameworkCore;

namespace PartnerService;

// === REQUEST / RESPONSE RECORDS ===

public record CreatePartnerRequest(
    string  FullName,
    string  ShortName,
    string  EntityType,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxCode
);

public record UpdatePartnerRequest(
    string  FullName,
    string  ShortName,
    string? EntityType,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxCode
);

public record PartnerFilter(
    string? SearchTerm,
    string? EntityType,
    bool?   IsActive,
    int     PageNumber = 1,
    int     PageSize   = 10
);

public record PagedResult<T>(
    List<T> Items,
    int     TotalCount,
    int     PageNumber,
    int     PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

// === INTERFACE ===

public interface IPartnerBusinessService
{
    Task<Partner>              CreateAsync(CreatePartnerRequest req, Guid userId, string? userRole);
    Task<Partner>              UpdateAsync(Guid id, UpdatePartnerRequest req, Guid userId, string? userRole);
    Task<Partner?>             GetByIdAsync(Guid id);
    Task<PagedResult<Partner>> GetListAsync(PartnerFilter filter);
    Task<bool>                 SoftDeleteAsync(Guid id, string? userRole);
    Task<Partner>              RestoreAsync(Guid id, string? userRole);
}

// === IMPLEMENTATION ===

public class PartnerBusinessService : IPartnerBusinessService
{
    private readonly PartnerDbContext _db;

    public PartnerBusinessService(PartnerDbContext db)
    {
        _db = db;
    }

    // ────────────────────────────────────────────
    // HELPERS
    // ────────────────────────────────────────────

    private static void AssertWriteRole(string? userRole)
    {
        if (userRole != "Admin" && userRole != "Secretary" && userRole != "SecretaryDirector")
            throw new UnauthorizedAccessException(
                $"Ban khong co quyen thuc hien thao tac nay. " +
                $"Chi vai tro 'Admin', 'Secretary' hoac 'SecretaryDirector' duoc phep. " +
                $"Vai tro hien tai cua ban: '{userRole ?? "khong xac dinh"}'.");
    }

    private static void AssertDirectorSecretaryRole(string? userRole)
    {
        if (userRole != "Admin" && userRole != "SecretaryDirector")
            throw new UnauthorizedAccessException(
                $"Ban khong co quyen khoi phuc doi tac. " +
                $"Chi vai tro 'Admin' hoac 'SecretaryDirector' duoc phep. " +
                $"Vai tro hien tai cua ban: '{userRole ?? "khong xac dinh"}'.");
    }

    private static void ValidateEntityType(string entityType)
    {
        if (!PartnerEntityTypeConstants.AllValues.Contains(entityType))
            throw new ArgumentException(
                $"EntityType '{entityType}' khong hop le. " +
                $"Cac gia tri cho phep: {string.Join(", ", PartnerEntityTypeConstants.AllValues)}.");
    }

    private static void ValidateEmailAndPhone(string? email, string? phone)
    {
        if (!string.IsNullOrWhiteSpace(email) && (!email.Contains("@") || !email.Contains(".")))
            throw new ArgumentException("Dinh dang Email khong hop le.");

        if (!string.IsNullOrWhiteSpace(phone) && phone.Trim().Length < 8)
            throw new ArgumentException("Dinh dang So dien thoai khong hop le (can it nhat 8 chu so).");
    }

    // ────────────────────────────────────────────
    // CREATE
    // ────────────────────────────────────────────

    public async Task<Partner> CreateAsync(CreatePartnerRequest req, Guid userId, string? userRole)
    {
        AssertWriteRole(userRole);

        if (string.IsNullOrWhiteSpace(req.FullName))
            throw new ArgumentException("Ten day du (FullName) khong duoc de trong.");

        if (string.IsNullOrWhiteSpace(req.ShortName))
            throw new ArgumentException("Ten viet tat (ShortName) khong duoc de trong.");

        ValidateEntityType(req.EntityType);
        ValidateEmailAndPhone(req.Email, req.Phone);

        // Kiem tra ShortName unique
        var existsByShortName = await _db.Partners
            .IgnoreQueryFilters()
            .AnyAsync(p => p.ShortName == req.ShortName.Trim());
        if (existsByShortName)
            throw new ArgumentException(
                $"Ten viet tat '{req.ShortName}' da ton tai trong he thong. Vui long chon ten khac.");

        // Kiem tra TaxCode unique neu co
        if (!string.IsNullOrWhiteSpace(req.TaxCode))
        {
            var existsByTaxCode = await _db.Partners
                .IgnoreQueryFilters()
                .AnyAsync(p => p.TaxCode == req.TaxCode.Trim());
            if (existsByTaxCode)
                throw new ArgumentException(
                    $"Ma so thue '{req.TaxCode}' da ton tai trong he thong.");
        }

        var partner = new Partner
        {
            FullName        = req.FullName.Trim(),
            ShortName       = req.ShortName.Trim(),
            EntityType      = req.EntityType,
            Email           = req.Email?.Trim(),
            Phone           = req.Phone?.Trim(),
            Address         = req.Address?.Trim(),
            TaxCode         = string.IsNullOrWhiteSpace(req.TaxCode) ? null : req.TaxCode.Trim(),
            CreatedByUserId = userId,
            CreatedAt       = DateTime.UtcNow
        };

        _db.Partners.Add(partner);
        await _db.SaveChangesAsync();
        return partner;
    }

    // ────────────────────────────────────────────
    // UPDATE
    // ────────────────────────────────────────────

    public async Task<Partner> UpdateAsync(Guid id, UpdatePartnerRequest req, Guid userId, string? userRole)
    {
        AssertWriteRole(userRole);

        var partner = await _db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null)
            throw new KeyNotFoundException($"Khong tim thay doi tac voi ID: {id}");

        if (string.IsNullOrWhiteSpace(req.FullName))
            throw new ArgumentException("Ten day du (FullName) khong duoc de trong.");

        if (string.IsNullOrWhiteSpace(req.ShortName))
            throw new ArgumentException("Ten viet tat (ShortName) khong duoc de trong.");

        if (req.EntityType != null)
            ValidateEntityType(req.EntityType);

        ValidateEmailAndPhone(req.Email, req.Phone);

        // Kiem tra ShortName unique (ngoai tru chinh no)
        if (req.ShortName.Trim() != partner.ShortName)
        {
            var exists = await _db.Partners
                .IgnoreQueryFilters()
                .AnyAsync(p => p.ShortName == req.ShortName.Trim() && p.Id != id);
            if (exists)
                throw new ArgumentException(
                    $"Ten viet tat '{req.ShortName}' da ton tai trong he thong.");
        }

        // Kiem tra TaxCode unique neu co (ngoai tru chinh no)
        if (!string.IsNullOrWhiteSpace(req.TaxCode) && req.TaxCode.Trim() != partner.TaxCode)
        {
            var existsByTaxCode = await _db.Partners
                .IgnoreQueryFilters()
                .AnyAsync(p => p.TaxCode == req.TaxCode.Trim() && p.Id != id);
            if (existsByTaxCode)
                throw new ArgumentException(
                    $"Ma so thue '{req.TaxCode}' da ton tai trong he thong.");
        }

        partner.FullName   = req.FullName.Trim();
        partner.ShortName  = req.ShortName.Trim();
        if (req.EntityType != null) partner.EntityType = req.EntityType;
        partner.Email      = req.Email?.Trim();
        partner.Phone      = req.Phone?.Trim();
        partner.Address    = req.Address?.Trim();
        partner.TaxCode    = string.IsNullOrWhiteSpace(req.TaxCode) ? null : req.TaxCode.Trim();
        partner.UpdatedAt  = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return partner;
    }

    // ────────────────────────────────────────────
    // GET BY ID
    // ────────────────────────────────────────────

    public async Task<Partner?> GetByIdAsync(Guid id)
    {
        return await _db.Partners
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // ────────────────────────────────────────────
    // GET LIST (paged)
    // ────────────────────────────────────────────

    public async Task<PagedResult<Partner>> GetListAsync(PartnerFilter filter)
    {
        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var query = _db.Partners.AsQueryable(); // QueryFilter tu dong bo IsDeleted

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.FullName.ToLower().Contains(term) ||
                p.ShortName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(p => p.EntityType == filter.EntityType);

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Partner>(items, totalCount, pageNumber, pageSize);
    }

    // ────────────────────────────────────────────
    // SOFT DELETE
    // ────────────────────────────────────────────

    public async Task<bool> SoftDeleteAsync(Guid id, string? userRole)
    {
        AssertWriteRole(userRole);

        var partner = await _db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null) return false;

        partner.IsDeleted = true;
        partner.DeletedAt = DateTime.UtcNow;
        partner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ────────────────────────────────────────────
    // RESTORE
    // ────────────────────────────────────────────

    public async Task<Partner> RestoreAsync(Guid id, string? userRole)
    {
        AssertDirectorSecretaryRole(userRole);

        var partner = await _db.Partners
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (partner == null)
            throw new KeyNotFoundException($"Khong tim thay doi tac voi ID: {id}");

        if (!partner.IsDeleted)
            throw new ArgumentException("Doi tac nay chua bi xoa, khong can khoi phuc.");

        partner.IsDeleted = false;
        partner.DeletedAt = null;
        partner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return partner;
    }
}
