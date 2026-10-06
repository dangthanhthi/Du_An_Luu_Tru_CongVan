using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

[Trait("Suite", "LegacyBaseline")]
public sealed class DocumentStatusTests
{
    private static readonly Guid Department = Guid.NewGuid();
    [Fact]
    public async Task Draft_can_be_marked_reviewed()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var document = Draft();
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var result = await Service(db).ChangeStatusAsync(
            document.Id, new ChangeStatusRequest(DocumentStatusConstants.Reviewed, null), Actor());

        Assert.Equal(DocumentStatusConstants.Reviewed, result.Status);
        Assert.Single(result.StatusHistories, h => h.NewStatus == DocumentStatusConstants.Reviewed);
    }

    [Fact]
    public async Task Repeating_review_is_idempotent()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var document = Draft();
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var service = Service(db);

        await service.ChangeStatusAsync(
            document.Id, new ChangeStatusRequest(DocumentStatusConstants.Reviewed, null), Actor());
        var result = await service.ChangeStatusAsync(
            document.Id, new ChangeStatusRequest(DocumentStatusConstants.Reviewed, null), Actor());

        Assert.Equal(DocumentStatusConstants.Reviewed, result.Status);
        Assert.Single(result.StatusHistories, h => h.NewStatus == DocumentStatusConstants.Reviewed);
    }

    [Fact]
    public async Task Nonexistent_document_returns_not_found_error()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).ChangeStatusAsync(
            Guid.NewGuid(), new ChangeStatusRequest(DocumentStatusConstants.Reviewed, null), Actor()));
    }

    [Fact]
    public async Task Stale_tracked_document_returns_conflict_error()
    {
        var databaseName = Guid.NewGuid().ToString();
        var document = Draft();
        await using (var seed = CreateDb(databaseName))
        {
            seed.Documents.Add(document);
            await seed.SaveChangesAsync();
        }

        await using var staleDb = CreateDb(databaseName);
        _ = await staleDb.Documents.SingleAsync(d => d.Id == document.Id);

        await using (var concurrentDb = CreateDb(databaseName))
        {
            var concurrent = await concurrentDb.Documents.SingleAsync(d => d.Id == document.Id);
            concurrent.Status = DocumentStatusConstants.Reviewed;
            concurrent.UpdatedAt = DateTime.UtcNow;
            await concurrentDb.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DocumentConcurrencyException>(() => Service(staleDb).ChangeStatusAsync(
            document.Id, new ChangeStatusRequest(DocumentStatusConstants.Reviewed, null), Actor()));
    }

    private static DocumentDbContext CreateDb(string name) => new(
        new DbContextOptionsBuilder<DocumentDbContext>().UseInMemoryDatabase(name).Options);

    private static DocumentBusinessService Service(DocumentDbContext db) =>
        new(db, new NotificationClient(), new PartnerClient(), new FileClient());

    private static DocumentActor Actor() =>
        new(Guid.NewGuid(), Department, new HashSet<string>(StringComparer.Ordinal) { "SecretaryDept" });

    private static Document Draft() => new()
    {
        DocumentNumber = Guid.NewGuid().ToString("N"),
        DocType = DocumentTypeConstants.INTERNAL,
        SenderDepartmentId = Department,
        Status = DocumentStatusConstants.Draft,
        Title = "Test document",
        CreatedByUserId = Guid.NewGuid()
    };

    private sealed class PartnerClient : IPartnerServiceClient
    {
        public Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId) => Task.FromResult<PartnerDto?>(null);
    }

    private sealed class FileClient : IFilesServiceClient
    {
        public Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId) => Task.FromResult<FileMetadataDto?>(null);
    }

    private sealed class NotificationClient : INotificationServiceClient
    {
        public Task<bool> SendNotificationAsync(SendNotificationRequest request) => Task.FromResult(true);
    }
}
