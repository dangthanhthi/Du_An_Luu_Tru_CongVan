using Microsoft.EntityFrameworkCore;

namespace DocumentService;

internal static class V2MutationLocks
{
    // Coarse graph lock prevents opposite-end mutations from taking aggregate locks in a cycle.
    // sp_getapplock scopes resources to this database/installation and transaction.
    public static Task RelationsAsync(DocumentDbContext db, CancellationToken ct) => AcquireAsync(db, "DAS:v2:relations", ct);
    public static Task DocumentAsync(DocumentDbContext db, Guid id, CancellationToken ct) => AcquireAsync(db, "DAS:v2:edit:" + id.ToString("N"), ct);
    private static async Task AcquireAsync(DocumentDbContext db, string name, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={name}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51003, 'Document mutation lock was unavailable.', 1;
            """, ct);
    }
}
