using Microsoft.EntityFrameworkCore;

namespace DocumentService;

public class DocumentBusinessService : IDocumentBusinessService
{
    private readonly DocumentDbContext _db;
    private readonly INotificationServiceClient _notificationClient;
    private readonly IPartnerServiceClient _partnerClient;
    private readonly IFilesServiceClient _filesClient;
    private readonly TimeProvider _clock;
    private IQueryable<Document> LegacyDocuments => _db.Documents.Where(d => d.Registration == null);

    public DocumentBusinessService(
        DocumentDbContext db,
        INotificationServiceClient notificationClient,
        IPartnerServiceClient partnerClient,
        IFilesServiceClient filesClient,
        TimeProvider? clock = null)
    {
        _db = db;
        _notificationClient = notificationClient;
        _partnerClient = partnerClient;
        _filesClient = filesClient;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Document> CreateIncomingAsync(CreateIncomingDocumentRequest req, DocumentActor actor)
    {
        if (!DocumentAccessRules.CanCreateIncoming(actor))
            throw new UnauthorizedAccessException("You do not have permission to register incoming documents.");

        var sourceMessageId = string.IsNullOrWhiteSpace(req.SourceMessageId) ? null : req.SourceMessageId.Trim();
        if (sourceMessageId is not null)
        {
            // Committed replays do not depend on references that may have changed since registration.
            // The writer repeats this check under the allocation lock for concurrent first requests.
            var registered = await LegacyDocuments.IgnoreQueryFilters().AsNoTracking()
                .Include(x => x.Attachments).Include(x => x.DepartmentAccesses).Include(x => x.StatusHistories)
                .SingleOrDefaultAsync(x => x.SourceMessageId == sourceMessageId);
            if (registered?.IsDeleted == true)
                throw new InvalidOperationException("This source message belongs to an existing deleted registration.");
            if (registered is not null) return registered;
        }
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        if (req.PartnerId.HasValue)
            await ValidateActivePartnerAsync(req.PartnerId.Value);
        await ValidateFilesAsync(req.AttachmentFileIds);

        var doc = new Document
        {
            DocType = DocumentTypeConstants.INCOMING,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = req.PartnerId,
            CreatedByUserId = actor.UserId,
            SourceMessageId = sourceMessageId,
            ReceivedAt = req.ReceivedAt,
            CreatedAt = DateTime.UtcNow
        };

        if (req.AttachmentFileIds != null)
        {
            foreach (var fileId in req.AttachmentFileIds)
            {
                doc.Attachments.Add(new DocumentAttachment
                {
                    DocumentId = doc.Id,
                    FileId = fileId,
                    AttachmentType = "Scan",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        doc.StatusHistories.Add(new DocumentStatusHistory
        {
            DocumentId = doc.Id,
            OldStatus = null,
            NewStatus = DocumentStatusConstants.Draft,
            ChangedByUserId = actor.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = "Khởi tạo công văn đến"
        });

        return await new DocumentRegistrationWriter(_db, _clock).RegisterLegacyAsync(doc);
    }

    public async Task<Document> CreateOutgoingAsync(CreateOutgoingDocumentRequest req, DocumentActor actor)
    {
        if (!DocumentAccessRules.CanCreateDepartmentDocument(actor))
            throw new UnauthorizedAccessException("You do not have permission to register outgoing documents.");
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");
        if (req.PartnerId == Guid.Empty)
            throw new ArgumentException("Đối tác nhận (PartnerId) không hợp lệ.");
        if (actor.IsInRole("SecretaryDept") && !actor.DepartmentId.HasValue)
            throw new UnauthorizedAccessException("A department secretary must belong to an active department.");

        await ValidateActivePartnerAsync(req.PartnerId);
        await ValidateFilesAsync(req.AttachmentFileIds);
        var senderDepartmentId = actor.IsInRole("SecretaryDept")
            ? actor.DepartmentId!.Value
            : req.SenderDepartmentId;

        var doc = new Document
        {
            DocType = DocumentTypeConstants.OUTGOING,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = req.PartnerId,
            SenderDepartmentId = senderDepartmentId,
            CreatedByUserId = actor.UserId,
            CreatedAt = DateTime.UtcNow
        };

        if (req.AttachmentFileIds != null)
        {
            foreach (var fileId in req.AttachmentFileIds)
            {
                doc.Attachments.Add(new DocumentAttachment
                {
                    DocumentId = doc.Id,
                    FileId = fileId,
                    AttachmentType = "Original",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        doc.StatusHistories.Add(new DocumentStatusHistory
        {
            DocumentId = doc.Id,
            OldStatus = null,
            NewStatus = DocumentStatusConstants.Draft,
            ChangedByUserId = actor.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = "Khởi tạo công văn đi"
        });

        return await new DocumentRegistrationWriter(_db, _clock).RegisterLegacyAsync(doc);
    }

    public async Task<Document> CreateInternalAsync(CreateInternalDocumentRequest req, DocumentActor actor)
    {
        if (!DocumentAccessRules.CanCreateDepartmentDocument(actor))
            throw new UnauthorizedAccessException("You do not have permission to register internal documents.");
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");
        if (actor.IsInRole("SecretaryDept") && !actor.DepartmentId.HasValue)
            throw new UnauthorizedAccessException("A department secretary must belong to an active department.");

        await ValidateFilesAsync(req.AttachmentFileIds);
        var senderDepartmentId = actor.IsInRole("SecretaryDept")
            ? actor.DepartmentId!.Value
            : req.SenderDepartmentId;

        var doc = new Document
        {
            DocType = DocumentTypeConstants.INTERNAL,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = null,
            SenderDepartmentId = senderDepartmentId,
            CreatedByUserId = actor.UserId,
            CreatedAt = DateTime.UtcNow
        };

        if (req.AttachmentFileIds != null)
        {
            foreach (var fileId in req.AttachmentFileIds)
            {
                doc.Attachments.Add(new DocumentAttachment
                {
                    DocumentId = doc.Id,
                    FileId = fileId,
                    AttachmentType = "Original",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        doc.StatusHistories.Add(new DocumentStatusHistory
        {
            DocumentId = doc.Id,
            OldStatus = null,
            NewStatus = DocumentStatusConstants.Draft,
            ChangedByUserId = actor.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = "Khởi tạo công văn nội bộ"
        });

        return await new DocumentRegistrationWriter(_db, _clock).RegisterLegacyAsync(doc);
    }

    public async Task<Document> UpdateAsync(Guid id, UpdateDocumentRequest req, DocumentActor actor)
    {
        var doc = await LegacyDocuments
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (!DocumentAccessRules.CanEdit(actor, doc))
            throw new UnauthorizedAccessException("You do not have permission to edit this document.");

        if (doc.Status != DocumentStatusConstants.Draft)
            throw new InvalidOperationException("Chỉ có thể chỉnh sửa công văn khi ở trạng thái 'Draft'.");

        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        if (req.SenderDepartmentId.HasValue && req.SenderDepartmentId != doc.SenderDepartmentId)
            throw new InvalidOperationException("The registered document department cannot be changed.");

        doc.Title = req.Title.Trim();
        doc.Summary = req.Summary?.Trim();
        if (req.PartnerId.HasValue)
        {
            await ValidateActivePartnerAsync(req.PartnerId.Value);
            doc.PartnerId = req.PartnerId.Value;
        }
        if (req.ReceivedAt.HasValue) doc.ReceivedAt = req.ReceivedAt.Value;
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<Document> ChangeStatusAsync(Guid id, ChangeStatusRequest req, DocumentActor actor)
    {
        var doc = await LegacyDocuments
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (!DocumentAccessRules.CanChangeStatus(actor, doc))
            throw new UnauthorizedAccessException("You do not have permission to change this document's status.");

        var oldStatus = doc.Status;
        var newStatus = req.Status;

        if (oldStatus == newStatus) return doc;

        // Valid transitions: Draft -> Reviewed, Draft -> Distributed, Reviewed -> Distributed
        bool isValidTransition = (oldStatus, newStatus) switch
        {
            (DocumentStatusConstants.Draft, DocumentStatusConstants.Reviewed) => true,
            (DocumentStatusConstants.Draft, DocumentStatusConstants.Distributed) => true,
            (DocumentStatusConstants.Reviewed, DocumentStatusConstants.Distributed) => true,
            _ => false
        };

        if (!isValidTransition)
            throw new InvalidOperationException($"Không thể chuyển trạng thái từ '{oldStatus}' sang '{newStatus}'.");

        if (newStatus == DocumentStatusConstants.Distributed)
            await ValidateReadyForDistributionAsync(doc);

        doc.Status = newStatus;
        doc.UpdatedAt = DateTime.UtcNow;

        if (newStatus == DocumentStatusConstants.Distributed)
        {
            doc.DistributedAt = DateTime.UtcNow;
            
            // Asynchronously notify via NotificationService (non-blocking)
        }

        var history = new DocumentStatusHistory
        {
            DocumentId = doc.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = actor.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = req.Note?.Trim()
        };
        doc.StatusHistories.Add(history);

        // The history key is generated client-side. Explicitly mark this entity as
        // Added so EF never interprets its non-default Guid as an existing row and
        // emits an UPDATE that affects zero rows.
        _db.DocumentStatusHistory.Add(history);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Do not retry with refreshed values: that would silently overwrite the
            // concurrent change and could create an incorrect status-history entry.
            var current = await LegacyDocuments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => new { d.IsDeleted })
                .SingleOrDefaultAsync();

            if (current == null || current.IsDeleted)
                throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}", exception);

            throw new DocumentConcurrencyException(
                "Công văn đã được thay đổi bởi một yêu cầu khác. Vui lòng tải lại và thử lại.",
                exception);
        }
        return doc;
    }

    public async Task<Document> AssignAccessAsync(Guid id, AssignAccessRequest req, DocumentActor actor)
    {
        var doc = await LegacyDocuments
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (!DocumentAccessRules.CanAssignDepartments(actor, doc))
            throw new UnauthorizedAccessException("You do not have permission to assign recipient departments.");

        if (req.DepartmentIds == null || req.DepartmentIds.Count == 0)
            throw new ArgumentException("Vui lòng chọn ít nhất một phòng ban để phân quyền.");

        var requestedDepartments = req.DepartmentIds.Distinct().ToHashSet();
        _db.DocumentDepartmentAccess.RemoveRange(
            doc.DepartmentAccesses.Where(access => !requestedDepartments.Contains(access.DepartmentId)));

        foreach (var deptId in requestedDepartments)
        {
            if (!doc.DepartmentAccesses.Any(a => a.DepartmentId == deptId))
            {
                doc.DepartmentAccesses.Add(new DocumentDepartmentAccess
                {
                    DocumentId = doc.Id,
                    DepartmentId = deptId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = actor.UserId
                });
            }
        }

        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task<Document> AddAttachmentAsync(Guid id, AddAttachmentRequest req, DocumentActor actor)
    {
        var doc = await LegacyDocuments
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (!DocumentAccessRules.CanEdit(actor, doc))
            throw new UnauthorizedAccessException("You do not have permission to add attachments to this document.");

        if (req.FileId == Guid.Empty)
            throw new ArgumentException("FileId không hợp lệ.");

        await ValidateFilesAsync([req.FileId]);

        var attachment = new DocumentAttachment
        {
            DocumentId = doc.Id,
            FileId = req.FileId,
            AttachmentType = req.AttachmentType ?? "Reference",
            CreatedAt = DateTime.UtcNow
        };

        // Adding an attachment is a child-row insert. Do not touch the parent
        // document: doing so creates an unnecessary concurrency-checked UPDATE.
        // Explicitly add the child so its client-generated Guid cannot be mistaken
        // for the key of an existing (detached) attachment.
        _db.DocumentAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task RemoveAttachmentAsync(Guid id, Guid attachmentId, DocumentActor actor)
    {
        var doc = await LegacyDocuments
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (!DocumentAccessRules.CanEdit(actor, doc))
            throw new UnauthorizedAccessException("You do not have permission to remove attachments from this document.");

        var attachment = doc.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment != null)
        {
            _db.DocumentAttachments.Remove(attachment);
            doc.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Document?> GetByIdAsync(Guid id, DocumentActor actor)
    {
        var query = LegacyDocuments
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .Where(d => d.Id == id);

        var doc = await ApplyReadScope(query, actor).FirstOrDefaultAsync();

        if (doc is null && await LegacyDocuments.AnyAsync(d => d.Id == id))
            throw new UnauthorizedAccessException("You do not have permission to access this document.");

        return doc;
    }

    public async Task<PagedResult<Document>> GetListAsync(DocumentFilter filter, DocumentActor actor)
    {
        var query = LegacyDocuments
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .AsQueryable();

        query = ApplyReadScope(query, actor);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(d => d.DocumentNumber.ToLower().Contains(term)
                                  || d.Title.ToLower().Contains(term)
                                  || (d.Summary != null && d.Summary.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.DocType))
            query = query.Where(d => d.DocType == filter.DocType);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(d => d.Status == filter.Status);

        if (filter.PartnerId.HasValue)
            query = query.Where(d => d.PartnerId == filter.PartnerId.Value);

        if (filter.DepartmentId.HasValue)
        {
            var departmentId = filter.DepartmentId.Value;
            query = query.Where(d =>
                d.DocType == DocumentTypeConstants.INCOMING
                    ? d.DepartmentAccesses.Any(a => a.DepartmentId == departmentId)
                    : d.SenderDepartmentId == departmentId);
        }

        if (filter.FromDate.HasValue)
            query = query.Where(d => d.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(d => d.CreatedAt <= filter.ToDate.Value);

        var totalCount = await query.CountAsync();

        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Document>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<bool> DeleteAsync(Guid id, DocumentActor actor)
    {
        var doc = await LegacyDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return false;

        if (!DocumentAccessRules.CanEdit(actor, doc))
            throw new UnauthorizedAccessException("You do not have permission to delete this document.");

        // Chỉ cho phép xóa công văn khi ở trạng thái Draft
        if (doc.Status != DocumentStatusConstants.Draft)
            throw new InvalidOperationException(
                $"Không thể xóa công văn khi ở trạng thái '{doc.Status}'. Chỉ công văn 'Draft' mới được phép xóa.");

        // Soft Delete: đánh dấu xóa thay vì xóa cứng khỏi database
        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool?> CanReadFileAsync(Guid fileId, DocumentActor actor)
    {
        var documentsWithFile = _db.Documents.Where(d => d.Attachments.Any(a => a.FileId == fileId));
        if (!await documentsWithFile.AnyAsync()) return null;

        return await ApplyReadScope(documentsWithFile, actor).AnyAsync();
    }

    public Task<bool> HasProcessedSourceMessageAsync(string sourceMessageId) =>
        string.IsNullOrWhiteSpace(sourceMessageId) ? Task.FromResult(false) :
        LegacyDocuments.IgnoreQueryFilters().AnyAsync(document => document.SourceMessageId == sourceMessageId.Trim());

    private static IQueryable<Document> ApplyReadScope(IQueryable<Document> query, DocumentActor actor)
    {
        query = query.Where(d => d.Registration == null);
        if (actor.IsInRole("SecretaryDirector"))
            return query.Where(d => d.DocType == DocumentTypeConstants.INCOMING ||
                                    d.DocType == DocumentTypeConstants.INTERNAL &&
                                    d.Status == DocumentStatusConstants.Distributed);
        if (!actor.DepartmentId.HasValue) return query.Where(_ => false);

        var departmentId = actor.DepartmentId.Value;
        if (actor.IsInRole("SecretaryDept"))
            return query.Where(d =>
                d.DocType == DocumentTypeConstants.INCOMING && d.DepartmentAccesses.Any(a => a.DepartmentId == departmentId) ||
                (d.DocType == DocumentTypeConstants.OUTGOING || d.DocType == DocumentTypeConstants.INTERNAL) &&
                d.SenderDepartmentId == departmentId);
        if (actor.IsInRole("Staff"))
            return query.Where(d => d.DocType == DocumentTypeConstants.INCOMING &&
                                    d.DepartmentAccesses.Any(a => a.DepartmentId == departmentId));

        return query.Where(_ => false);
    }

    private async Task ValidateActivePartnerAsync(Guid partnerId)
    {
        var partner = await _partnerClient.GetPartnerByIdAsync(partnerId);
        if (partner is null || !partner.IsActive)
            throw new ArgumentException("PartnerId must reference an active External Entity.");
    }

    private async Task ValidateFilesAsync(IEnumerable<Guid>? fileIds)
    {
        if (fileIds is null) return;
        foreach (var fileId in fileIds.Distinct())
        {
            if (fileId == Guid.Empty || await _filesClient.GetFileByIdAsync(fileId) is null)
                throw new ArgumentException($"Attachment file '{fileId}' does not exist.");
        }
    }

    private async Task ValidateReadyForDistributionAsync(Document document)
    {
        if (document.Attachments.Count == 0)
            throw new InvalidOperationException("A PDF attachment is required before distribution.");
        if (document.DocType == DocumentTypeConstants.OUTGOING)
        {
            if (!document.PartnerId.HasValue)
                throw new InvalidOperationException("An active recipient External Entity is required before distribution.");
            await ValidateActivePartnerAsync(document.PartnerId.Value);
        }
        if (document.DocType == DocumentTypeConstants.INCOMING && document.DepartmentAccesses.Count == 0)
            throw new InvalidOperationException("At least one recipient department is required before distribution.");
    }
}
