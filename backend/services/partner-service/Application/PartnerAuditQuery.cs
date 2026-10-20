using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace PartnerService;

public sealed record PartnerAuditItem(Guid Id, Guid ActorUserId, string Action, string Version, DateTime OccurredAt, string ChangesAvailability = "NotRecorded");
public sealed record PartnerAuditPage(Guid PartnerId, string ThroughVersion, IReadOnlyList<PartnerAuditItem> Items, int TotalCount, int PageNumber, int PageSize);

public sealed class PartnerAuditQuery(PartnerDbContext db)
{
    public async Task<PartnerAuditPage> ReadAsync(Guid id, int page, int size, long? watermark, CancellationToken ct)
    {
        if (id == Guid.Empty || page < 1 || page > 1_000_000 || size < 1 || size > 100 || watermark is <= 0 || (page > 1 && watermark is null))
            throw new PartnerRuleException(400,"INVALID_HISTORY_QUERY","Tham số lịch sử không hợp lệ.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var version = await db.Partners.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct);
            if (version is null) throw new PartnerRuleException(404,"PARTNER_NOT_FOUND","Không tìm thấy đơn vị.");
            if (version <= 0) throw InvalidData();
            if (watermark > version) throw new PartnerRuleException(400,"INVALID_HISTORY_QUERY","Tham số lịch sử không hợp lệ.");
            var through = watermark ?? version.Value;
            var query = db.PartnerAudits.AsNoTracking().Where(x => x.PartnerId == id && x.Version <= through);
            if (await query.AnyAsync(x => x.Id == Guid.Empty || x.ActorUserId == Guid.Empty || x.Version <= 0 || x.CreatedAt == DateTime.MinValue ||
                (x.Action != "Create" && x.Action != "Update" && x.Action != "Delete" && x.Action != "Restore"),ct)) throw InvalidData();
            var count = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(x => x.Version).ThenBy(x => x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
            // PartnerBusinessService is the verified source of these audit rows and writes DateTime.UtcNow.
            // datetime2/SQLite do not preserve Kind; restoring this established UTC contract is not a legacy timezone inference.
            if (rows.Any(x => x.CreatedAt.Kind == DateTimeKind.Local)) throw InvalidData();
            var items = rows.Select(x => new PartnerAuditItem(x.Id,x.ActorUserId,x.Action,x.Version.ToString(CultureInfo.InvariantCulture),DateTime.SpecifyKind(x.CreatedAt,DateTimeKind.Utc))).ToArray();
            await transaction.CommitAsync(ct);
            return new PartnerAuditPage(id,through.ToString(CultureInfo.InvariantCulture),items,count,page,size);
        });
    }
    private static PartnerRuleException InvalidData() => new(503,"HISTORY_DATA_INVALID","Dữ liệu lịch sử chưa thể xác minh.");
}
