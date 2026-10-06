using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace DocumentService.Tests;

[Trait("Suite", "LegacyBaseline")]
public sealed class AuthorizationTests
{
    private static readonly Guid ItDepartment = Guid.NewGuid();
    private static readonly Guid HrDepartment = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();
    private static readonly Guid FileId = Guid.NewGuid();

    [Fact]
    public async Task Department_list_is_scoped_in_database_query()
    {
        await using var db = CreateDb();
        db.Documents.AddRange(
            Incoming(ItDepartment),
            Incoming(HrDepartment),
            DepartmentDocument(DocumentTypeConstants.OUTGOING, ItDepartment),
            DepartmentDocument(DocumentTypeConstants.INTERNAL, HrDepartment));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetListAsync(EmptyFilter(), Actor("SecretaryDept", ItDepartment));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, document => Assert.True(
            document.SenderDepartmentId == ItDepartment ||
            document.DepartmentAccesses.Any(access => access.DepartmentId == ItDepartment)));
    }

    [Fact]
    public async Task Incoming_visibility_uses_only_recipient_department_for_list_detail_and_attachments()
    {
        await using var db = CreateDb();
        var itFile = Guid.NewGuid();
        var hrFile = Guid.NewGuid();
        var unassignedFile = Guid.NewGuid();
        var documentA = Incoming(ItDepartment, "Document A", itFile);
        var documentB = Incoming(HrDepartment, "Document B", hrFile);
        var documentC = NewDocument(DocumentTypeConstants.INCOMING, ItDepartment, "Document C");
        documentC.Attachments.Add(new DocumentAttachment { FileId = unassignedFile });
        db.Documents.AddRange(documentA, documentB, documentC);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var incomingFilter = EmptyFilter() with { DocType = DocumentTypeConstants.INCOMING };

        await AssertVisibility(service, incomingFilter, Actor("Admin", null),
            [documentA, documentB, documentC], [itFile, hrFile, unassignedFile]);
        await AssertVisibility(service, incomingFilter, Actor("SecretaryDirector", null),
            [documentA, documentB, documentC], [itFile, hrFile, unassignedFile]);
        await AssertVisibility(service, incomingFilter, Actor("Staff", ItDepartment),
            [documentA], [itFile], [documentB, documentC], [hrFile, unassignedFile]);
        await AssertVisibility(service, incomingFilter, Actor("SecretaryDept", ItDepartment),
            [documentA], [itFile], [documentB, documentC], [hrFile, unassignedFile]);
        await AssertVisibility(service, incomingFilter, Actor("Staff", HrDepartment),
            [documentB], [hrFile], [documentA, documentC], [itFile, unassignedFile]);
        await AssertVisibility(service, incomingFilter, Actor("SecretaryDept", HrDepartment),
            [documentB], [hrFile], [documentA, documentC], [itFile, unassignedFile]);
    }

    [Fact]
    public async Task Incoming_department_filter_does_not_use_sender_department()
    {
        await using var db = CreateDb();
        var wrongSenderDepartment = NewDocument(DocumentTypeConstants.INCOMING, ItDepartment, "Unassigned");
        var hrRecipient = Incoming(HrDepartment, "HR recipient");
        hrRecipient.SenderDepartmentId = ItDepartment;
        db.Documents.AddRange(wrongSenderDepartment, hrRecipient);
        await db.SaveChangesAsync();

        var filter = EmptyFilter() with
        {
            DocType = DocumentTypeConstants.INCOMING,
            DepartmentId = ItDepartment
        };
        var result = await CreateService(db).GetListAsync(filter, Actor("Admin", null));

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Current_actor_department_is_resolved_from_jwt_department_claim()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("departmentId", ItDepartment.ToString()),
            new Claim(ClaimTypes.Role, "Staff")
        ], "Bearer"));

        var actor = DocumentActor.FromPrincipal(principal);

        Assert.Equal(userId, actor.UserId);
        Assert.Equal(ItDepartment, actor.DepartmentId);
        Assert.True(actor.IsInRole("Staff"));
    }

    [Fact]
    public async Task Cross_department_detail_and_mutation_are_denied()
    {
        await using var db = CreateDb();
        var document = DepartmentDocument(DocumentTypeConstants.OUTGOING, HrDepartment);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var actor = Actor("SecretaryDept", ItDepartment);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(document.Id, actor));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(
            document.Id, new UpdateDocumentRequest("Changed", null, null, ItDepartment, null), actor));
    }

    [Fact]
    public async Task Staff_cannot_create_or_change_status()
    {
        await using var db = CreateDb();
        var document = Incoming(ItDepartment);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var staff = Actor("Staff", ItDepartment);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateOutgoingAsync(
            new CreateOutgoingDocumentRequest("Outgoing", null, PartnerId, ItDepartment, [FileId]), staff));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChangeStatusAsync(
            document.Id, new ChangeStatusRequest(DocumentStatusConstants.Distributed, null), staff));
    }

    [Fact]
    public async Task Secretary_department_is_derived_from_claim_not_request()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var document = await service.CreateOutgoingAsync(
            new CreateOutgoingDocumentRequest("Outgoing", null, PartnerId, HrDepartment, [FileId]),
            Actor("SecretaryDept", ItDepartment));

        Assert.Equal(ItDepartment, document.SenderDepartmentId);
    }

    [Fact]
    public async Task Distributed_transition_requires_attachment()
    {
        await using var db = CreateDb();
        var document = DepartmentDocument(DocumentTypeConstants.INTERNAL, ItDepartment);
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(db).ChangeStatusAsync(
            document.Id,
            new ChangeStatusRequest(DocumentStatusConstants.Distributed, null),
            Actor("SecretaryDept", ItDepartment)));
    }

    [Fact]
    public async Task Attachment_access_uses_parent_document_scope()
    {
        await using var db = CreateDb();
        var document = Incoming(ItDepartment);
        document.Attachments.Add(new DocumentAttachment { FileId = FileId });
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        Assert.True(await service.CanReadFileAsync(FileId, Actor("Staff", ItDepartment)));
        Assert.False(await service.CanReadFileAsync(FileId, Actor("Staff", HrDepartment)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Nonexistent_or_inactive_partner_is_rejected(bool partnerExists, bool partnerActive)
    {
        await using var db = CreateDb();
        var service = CreateService(db, partnerExists, partnerActive);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOutgoingAsync(
            new CreateOutgoingDocumentRequest("Outgoing", null, PartnerId, ItDepartment, [FileId]),
            Actor("SecretaryDept", ItDepartment)));
    }

    [Fact]
    public async Task Duplicate_source_message_returns_existing_document()
    {
        await using var db = CreateDb();
        var existing = Incoming(ItDepartment);
        existing.SourceMessageId = "<fax-123@example.test>";
        db.Documents.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateIncomingAsync(
            new CreateIncomingDocumentRequest("Duplicate", null, null, DateTime.UtcNow, null, existing.SourceMessageId),
            Actor("System", null));

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(1, await db.Documents.CountAsync());
    }

    private static DocumentDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DocumentDbContext(options);
    }

    private static DocumentBusinessService CreateService(
        DocumentDbContext db, bool partnerExists = true, bool partnerActive = true)
    {
        PartnerDto? partner = partnerExists
            ? new PartnerDto(PartnerId, "Partner", "P", "Both", null, null, partnerActive)
            : null;
        return new DocumentBusinessService(db, new NotificationClient(), new PartnerClient(partner), new FileClient());
    }

    private static DocumentActor Actor(string role, Guid? department) =>
        new(Guid.NewGuid(), department, new HashSet<string>(StringComparer.Ordinal) { role });

    private static Document Incoming(Guid recipientDepartment, string title = "Test", Guid? fileId = null)
    {
        var document = NewDocument(DocumentTypeConstants.INCOMING, null, title);
        document.DepartmentAccesses.Add(new DocumentDepartmentAccess
        {
            DocumentId = document.Id,
            DepartmentId = recipientDepartment,
            AssignedByUserId = Guid.NewGuid()
        });
        if (fileId.HasValue)
            document.Attachments.Add(new DocumentAttachment { FileId = fileId.Value });
        return document;
    }

    private static Document DepartmentDocument(string type, Guid department) => NewDocument(type, department);

    private static Document NewDocument(string type, Guid? department, string title = "Test") => new()
    {
        DocumentNumber = Guid.NewGuid().ToString("N"),
        DocType = type,
        Status = DocumentStatusConstants.Draft,
        Title = title,
        SenderDepartmentId = department,
        CreatedByUserId = Guid.NewGuid()
    };

    private static DocumentFilter EmptyFilter() => new(null, null, null, null, null, null, null, 1, 20);

    private static async Task AssertVisibility(
        DocumentBusinessService service,
        DocumentFilter filter,
        DocumentActor actor,
        IReadOnlyCollection<Document> visibleDocuments,
        IReadOnlyCollection<Guid> visibleFiles,
        IReadOnlyCollection<Document>? hiddenDocuments = null,
        IReadOnlyCollection<Guid>? hiddenFiles = null)
    {
        var result = await service.GetListAsync(filter, actor);
        Assert.Equal(visibleDocuments.Select(document => document.Id).Order(),
            result.Items.Select(document => document.Id).Order());

        foreach (var document in visibleDocuments)
            Assert.Equal(document.Id, (await service.GetByIdAsync(document.Id, actor))?.Id);
        foreach (var file in visibleFiles)
            Assert.True(await service.CanReadFileAsync(file, actor));

        foreach (var document in hiddenDocuments ?? [])
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(document.Id, actor));
        foreach (var file in hiddenFiles ?? [])
            Assert.False(await service.CanReadFileAsync(file, actor));
    }

    private sealed class PartnerClient(PartnerDto? partner) : IPartnerServiceClient
    {
        public Task<PartnerDto?> GetPartnerByIdAsync(Guid partnerId) => Task.FromResult(partner);
    }

    private sealed class FileClient : IFilesServiceClient
    {
        public Task<FileMetadataDto?> GetFileByIdAsync(Guid fileId) =>
            Task.FromResult<FileMetadataDto?>(new(fileId, "test.pdf", 10, "application/pdf"));
    }

    private sealed class NotificationClient : INotificationServiceClient
    {
        public Task<bool> SendNotificationAsync(SendNotificationRequest request) => Task.FromResult(true);
    }
}
