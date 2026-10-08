using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DocumentService;

public interface INumberingService
{
    Task<string> GenerateNumberAsync(string docType, string companyCode, string? departmentCode, CancellationToken ct = default);
}

public sealed class NumberingService(DocumentDbContext db) : INumberingService
{
    public async Task<string> GenerateNumberAsync(string docType, string companyCode, string? departmentCode, CancellationToken ct = default)
    {
        // Require explicit transaction or create one
        var hasTransaction = db.Database.CurrentTransaction != null;
        using var tx = hasTransaction ? null : await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            var year = DateTime.UtcNow.Year;
            
            // App lock for SQL Server (No-op in SQLite which already serializes writes)
            if (db.Database.IsSqlServer())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource={$"DAS:numbering:{docType}:{year}"}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                    IF @result < 0 THROW 51003, 'Document numbering lock was unavailable.', 1;
                    """, ct);
            }

            var counter = await db.DocumentNumberCounters
                .FirstOrDefaultAsync(c => c.DocType == docType && c.Year == year, ct);

            if (counter == null)
            {
                counter = new DocumentNumberCounter
                {
                    DocType = docType,
                    Year = year,
                    CurrentValue = 1
                };
                db.DocumentNumberCounters.Add(counter);
            }
            else
            {
                counter.CurrentValue++;
            }

            await db.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            // Format: YY-MM-xxxx/<Cty> or YY-MM-xxxx/INT/<Cty>/<Dept>
            var yy = (year % 100).ToString("D2");
            var mm = DateTime.UtcNow.Month.ToString("D2");
            var seq = counter.CurrentValue.ToString("D4"); // 4 digits

            if (docType.Equals("INTERNAL", StringComparison.OrdinalIgnoreCase))
            {
                // YY-MM-xxxx/INT/<cty>/<dept>
                return $"{yy}-{mm}-{seq}/INT/{companyCode.ToUpperInvariant()}/{departmentCode?.ToUpperInvariant()}";
            }
            else if (docType.Equals("OUTGOING", StringComparison.OrdinalIgnoreCase))
            {
                // YY-MM-xxxx/<cty>/<dept>
                return $"{yy}-{mm}-{seq}/{companyCode.ToUpperInvariant()}/{departmentCode?.ToUpperInvariant()}";
            }
            else // INCOMING or others
            {
                // YY-MM-xxxx/<Cty>
                return $"{yy}-{mm}-{seq}/{companyCode.ToUpperInvariant()}";
            }
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
    }
}
