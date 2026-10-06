using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentService.Tests;

[Trait("Suite", "LegacyBaseline")]
public sealed class AttachmentTests
{
    private static readonly Guid Department = Guid.NewGuid();
    [Fact]
    public async Task Valid_attachments_are_inserted_without_updating_parent_and_persist()
    {
        var databaseName = Guid.NewGuid().ToString();
        var document = Document();
        await using (var seed = Db(databaseName))
        {
            seed.Documents.Add(document);
            await seed.SaveChangesAsync();
        }

        var firstFile = Guid.NewGuid();
        var secondFile = Guid.NewGuid();
        await using (var request = Db(databaseName))
        {
            var service = Service(request, firstFile, secondFile);

            var firstResult = await service.AddAttachmentAsync(
                document.Id, new AddAttachmentRequest(firstFile, null), Actor());
            var secondResult = await service.AddAttachmentAsync(
                document.Id, new AddAttachmentRequest(secondFile, "Original"), Actor());

            Assert.Equal(2, secondResult.Attachments.Count);
            Assert.Contains(firstResult.Attachments, item => item.FileId == firstFile);
            Assert.Contains(secondResult.Attachments, item => item.FileId == secondFile);
            Assert.Null(secondResult.UpdatedAt);
        }

        await using var refresh = Db(databaseName);
        var persisted = await refresh.Documents
            .Include(item => item.Attachments)
            .SingleAsync(item => item.Id == document.Id);
        Assert.Equal(2, persisted.Attachments.Count);
        Assert.Null(persisted.UpdatedAt);
    }

    [Fact]
    public async Task Nonexistent_document_is_not_found()
    {
        await using var db = Db(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db, Guid.NewGuid()).AddAttachmentAsync(
            Guid.NewGuid(), new AddAttachmentRequest(Guid.NewGuid(), null), Actor()));
    }

    [Fact]
    public async Task Invalid_file_is_rejected_without_creating_attachment()
    {
        await using var db = Db(Guid.NewGuid().ToString());
        var document = Document();
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).AddAttachmentAsync(
            document.Id, new AddAttachmentRequest(Guid.NewGuid(), null), Actor()));

        Assert.Empty(await db.DocumentAttachments.ToListAsync());
    }

    private static DocumentDbContext Db(string name) => new(
        new DbContextOptionsBuilder<DocumentDbContext>().UseInMemoryDatabase(name).Options);

    private static DocumentBusinessService Service(DocumentDbContext db, params Guid[] validFiles) =>
        new(db, new NotificationClient(), new PartnerClient(), new FileClient(validFiles));

    private static DocumentActor Actor() =>
        new(Guid.NewGuid(), Department, new HashSet<string>(StringComparer.Ordinal) { "SecretaryDept" });

    private static Document Document() => new()
    {
        DocumentNumber = Guid.NewGuid().ToString("N"),
        DocType = DocumentTypeConstants.INTERNAL,
        SenderDepartmentId = Department,
        Status = DocumentStatusConstants.Draft,
        Title = "Attachment test",
        CreatedByUserId = Guid.NewGuid()
    };

    private sealed class FileClient(IEnumerable<Guid> validFiles) : IFilesServiceClient
    {
        private readonly HashSet<Guid> _validFiles = validFiles.ToHashSet();

        public Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId) => Task.FromResult(
            _validFiles.Contains(fileId)
                ? new FileMetadataDto(fileId, "test.pdf", 100, "application/pdf")
                : null);
    }

    private sealed class PartnerClient : IPartnerServiceClient
    {
        public Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId) =>
            Task.FromResult<PartnerDto?>(null);
    }

    private sealed class NotificationClient : INotificationServiceClient
    {
        public Task<bool> SendNotificationAsync(SendNotificationRequest request) => Task.FromResult(true);
    }
}
