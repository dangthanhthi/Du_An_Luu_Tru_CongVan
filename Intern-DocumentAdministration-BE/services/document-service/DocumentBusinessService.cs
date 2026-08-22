using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DocumentService;

public record CreateIncomingDocumentRequest(
    string Title,
    string? ReferenceNumber,
    string? Summary,
    Guid? PartnerId,
    DateTime? ReceivedAt,
    List<Guid>? AttachmentFileIds
);

public record CreateOutgoingDocumentRequest(
    string Title,
    string? ReferenceNumber,
    string? Summary,
    Guid PartnerId,
    Guid SenderDepartmentId,
    List<Guid>? AttachmentFileIds
);

public record CreateInternalDocumentRequest(
    string Title,
    string? ReferenceNumber,
    string? Summary,
    Guid SenderDepartmentId,
    List<Guid>? AttachmentFileIds
);

public record UpdateDocumentRequest(
    string Title,
    string? ReferenceNumber,
    string? Summary,
    Guid? PartnerId,
    Guid? SenderDepartmentId,
    DateTime? ReceivedAt
);

public record ChangeStatusRequest(
    string Status,
    string? Note
);

public record AssignAccessRequest(
    List<Guid> DepartmentIds
);

public record AddAttachmentRequest(
    Guid FileId,
    string? AttachmentType
);

public record DocumentFilter(
    string? SearchTerm,
    string? DocType,
    string? Status,
    Guid? PartnerId,
    Guid? DepartmentId,
    DateTime? FromDate,
    DateTime? ToDate,
    int PageNumber = 1,
    int PageSize = 10
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public interface IDocumentBusinessService
{
    Task<Document> CreateIncomingAsync(CreateIncomingDocumentRequest req, Guid userId);
    Task<Document> CreateOutgoingAsync(CreateOutgoingDocumentRequest req, Guid userId);
    Task<Document> CreateInternalAsync(CreateInternalDocumentRequest req, Guid userId);
    Task<Document> UpdateAsync(Guid id, UpdateDocumentRequest req, Guid userId, string? userRole);
    Task<Document> ChangeStatusAsync(Guid id, ChangeStatusRequest req, Guid userId, string? userRole);
    Task<Document> AssignAccessAsync(Guid id, AssignAccessRequest req, Guid userId, string? userRole);
    Task<Document> AddAttachmentAsync(Guid id, AddAttachmentRequest req, Guid userId, string? userRole);
    Task RemoveAttachmentAsync(Guid id, Guid attachmentId, Guid userId, string? userRole);
    Task<Document?> GetByIdAsync(Guid id, Guid? userDepartmentId, string? userRole);
    Task<PagedResult<Document>> GetListAsync(DocumentFilter filter, Guid? userDepartmentId, string? userRole);
    Task<bool> DeleteAsync(Guid id, Guid userId, string? userRole);
}

public class DocumentBusinessService : IDocumentBusinessService
{
    private readonly DocumentDbContext _db;
    private readonly IPartnerServiceClient _partnerClient;
    private readonly IFilesServiceClient _filesClient;
    private readonly INotificationServiceClient _notificationClient;
    private readonly IAuthServiceClient _authClient;

    public DocumentBusinessService(
        DocumentDbContext db,
        IPartnerServiceClient partnerClient,
        IFilesServiceClient filesClient,
        INotificationServiceClient notificationClient,
        IAuthServiceClient authClient)
    {
        _db = db;
        _partnerClient = partnerClient;
        _filesClient = filesClient;
        _notificationClient = notificationClient;
        _authClient = authClient;
    }

    private static readonly System.Threading.SemaphoreSlim _sqliteLock = new(1, 1);

    private async Task<string> GenerateDocumentNumberAsync(string docType)
    {
        var year = DateTime.UtcNow.Year;
        // Khởi tạo 0 — compiler không track assignment bên trong async lambda closure.
        // Giá trị sẽ luôn được gán lại trong mỗi nhánh trước khi sử dụng.
        int nextValue = 0;

        if (_db.Database.IsSqlServer())
        {
            // SQL Server (Production/Docker): UPDLOCK + HOLDLOCK ngăn chặn duplicate số công văn
            // Nếu SQL Server có transient error (deadlock, timeout), exception sẽ được ném ra —
            // KHÔNG fallback để tránh cấp 2 request cùng số công văn.
            var executionStrategy = _db.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var counter = await _db.DocumentNumberCounters
                        .FromSqlRaw(
                            "SELECT * FROM document.DocumentNumberCounters WITH (UPDLOCK, HOLDLOCK) WHERE DocType = {0} AND Year = {1}",
                            docType, year)
                        .FirstOrDefaultAsync();

                    if (counter == null)
                    {
                        counter = new DocumentNumberCounter
                        {
                            DocType = docType,
                            Year = year,
                            CurrentValue = 1
                        };
                        _db.DocumentNumberCounters.Add(counter);
                        nextValue = 1;
                    }
                    else
                    {
                        counter.CurrentValue += 1;
                        nextValue = counter.CurrentValue;
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        else
        {
            // SQLite (Local Dev / Integration Testing): Dùng SemaphoreSlim lock in-process ngăn race condition đa luồng
            await _sqliteLock.WaitAsync();
            try
            {
                var counter = await _db.DocumentNumberCounters
                    .FirstOrDefaultAsync(c => c.DocType == docType && c.Year == year);

                if (counter == null)
                {
                    counter = new DocumentNumberCounter
                    {
                        DocType = docType,
                        Year = year,
                        CurrentValue = 1
                    };
                    _db.DocumentNumberCounters.Add(counter);
                    nextValue = 1;
                }
                else
                {
                    counter.CurrentValue += 1;
                    nextValue = counter.CurrentValue;
                }
                await _db.SaveChangesAsync();
            }
            finally
            {
                _sqliteLock.Release();
            }
        }

        string prefix = docType switch
        {
            DocumentTypeConstants.INCOMING => "CV-DEN",
            DocumentTypeConstants.OUTGOING => "CV-DI",
            DocumentTypeConstants.INTERNAL => "CV-NB",
            _ => "CV"
        };

        return $"{prefix}-{year}-{nextValue:D4}";
    }

    public async Task<Document> CreateIncomingAsync(CreateIncomingDocumentRequest req, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        // Validate partner-service nếu có PartnerId
        if (req.PartnerId.HasValue)
        {
            var partner = await _partnerClient.GetPartnerByIdAsync(req.PartnerId.Value);
            if (partner == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đối tác với ID: {req.PartnerId.Value}");
            }
        }


        // Validate files-service nếu có đính kèm
        if (req.AttachmentFileIds != null)
        {
            foreach (var fileId in req.AttachmentFileIds)
            {
                var fileMeta = await _filesClient.GetFileByIdAsync(fileId);
                if (fileMeta == null)
                {
                    throw new KeyNotFoundException($"Không tìm thấy file đính kèm với ID: {fileId}");
                }
            }
        }

        var docNumber = await GenerateDocumentNumberAsync(DocumentTypeConstants.INCOMING);

        var doc = new Document
        {
            DocumentNumber = docNumber,
            ReferenceNumber = req.ReferenceNumber?.Trim(),
            DocType = DocumentTypeConstants.INCOMING,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = req.PartnerId,
            CreatedByUserId = userId,
            ReceivedAt = req.ReceivedAt ?? DateTime.UtcNow,
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
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Note = "Khởi tạo công văn đến"
        });

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task<Document> CreateOutgoingAsync(CreateOutgoingDocumentRequest req, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");
        if (req.PartnerId == Guid.Empty)
            throw new ArgumentException("Đối tác nhận (PartnerId) không hợp lệ.");
        if (req.SenderDepartmentId == Guid.Empty)
            throw new ArgumentException("Phòng ban gửi (SenderDepartmentId) không hợp lệ.");

        // Validate bắt buộc: công văn đi phải có đối tác hợp lệ
        var outgoingPartner = await _partnerClient.GetPartnerByIdAsync(req.PartnerId);
        if (outgoingPartner == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy đối tác với ID: {req.PartnerId}");
        }

        // Validate files-service nếu có đính kèm
        if (req.AttachmentFileIds != null)
        {
            foreach (var fileId in req.AttachmentFileIds)
            {
                var fileMeta = await _filesClient.GetFileByIdAsync(fileId);
                if (fileMeta == null)
                {
                    throw new KeyNotFoundException($"Không tìm thấy file đính kèm với ID: {fileId}");
                }
            }
        }

        var docNumber = await GenerateDocumentNumberAsync(DocumentTypeConstants.OUTGOING);

        var doc = new Document
        {
            DocumentNumber = docNumber,
            ReferenceNumber = req.ReferenceNumber?.Trim(),
            DocType = DocumentTypeConstants.OUTGOING,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = req.PartnerId,
            SenderDepartmentId = req.SenderDepartmentId,
            CreatedByUserId = userId,
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
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Note = "Khởi tạo công văn đi"
        });

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task<Document> CreateInternalAsync(CreateInternalDocumentRequest req, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");
        if (req.SenderDepartmentId == Guid.Empty)
            throw new ArgumentException("Phòng ban soạn thảo (SenderDepartmentId) không hợp lệ.");

        // Validate files-service nếu có đính kèm
        if (req.AttachmentFileIds != null)
        {
            foreach (var fileId in req.AttachmentFileIds)
            {
                var fileMeta = await _filesClient.GetFileByIdAsync(fileId);
                if (fileMeta == null)
                {
                    throw new KeyNotFoundException($"Không tìm thấy file đính kèm với ID: {fileId}");
                }
            }
        }

        var docNumber = await GenerateDocumentNumberAsync(DocumentTypeConstants.INTERNAL);

        var doc = new Document
        {
            DocumentNumber = docNumber,
            ReferenceNumber = req.ReferenceNumber?.Trim(),
            DocType = DocumentTypeConstants.INTERNAL,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = null,
            SenderDepartmentId = req.SenderDepartmentId,
            CreatedByUserId = userId,
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
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Note = "Khởi tạo công văn nội bộ"
        });

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task<Document> UpdateAsync(Guid id, UpdateDocumentRequest req, Guid userId, string? userRole)
    {
        var doc = await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (doc.Status != DocumentStatusConstants.Draft)
            throw new InvalidOperationException("Chỉ có thể chỉnh sửa công văn khi ở trạng thái 'Draft'.");

        bool isOwner = doc.CreatedByUserId == userId;
        bool isAuthorizedRole = userRole == "Admin" || userRole == "SecretaryDirector" || userRole == "Secretary";
        if (!isOwner && !isAuthorizedRole)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa công văn này.");
        }

        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        doc.Title = req.Title.Trim();
        doc.ReferenceNumber = req.ReferenceNumber?.Trim();
        doc.Summary = req.Summary?.Trim();
        if (req.PartnerId.HasValue)
        {
            var updatePartner = await _partnerClient.GetPartnerByIdAsync(req.PartnerId.Value);
            if (updatePartner == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đối tác với ID: {req.PartnerId.Value}");
            }
            doc.PartnerId = req.PartnerId.Value;
        }

        if (req.SenderDepartmentId.HasValue)
        {
            if (req.SenderDepartmentId.Value == Guid.Empty)
                throw new ArgumentException("SenderDepartmentId không hợp lệ.");
            doc.SenderDepartmentId = req.SenderDepartmentId.Value;
        }
        if (req.ReceivedAt.HasValue) doc.ReceivedAt = req.ReceivedAt.Value;
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<Document> ChangeStatusAsync(Guid id, ChangeStatusRequest req, Guid userId, string? userRole)
    {
        // Fix #3: Kiểm tra quyền — Admin, Secretary và SecretaryDirector được đổi trạng thái công văn
        if (userRole != "Admin" && userRole != "Secretary" && userRole != "SecretaryDirector")
            throw new UnauthorizedAccessException(
                $"Bạn không có quyền thay đổi trạng thái công văn. " +
                $"Chỉ vai trò 'Admin', 'Secretary' hoặc 'SecretaryDirector' được phép thực hiện hành động này. " +
                $"Vai trò hiện tại của bạn: '{userRole ?? "không xác định"}'.");

        var doc = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        var oldStatus = doc.Status;
        var newStatus = req.Status;

        if (oldStatus == newStatus) return doc;

        if (newStatus == DocumentStatusConstants.Distributed && userRole != "SecretaryDirector" && userRole != "Admin")
            throw new UnauthorizedAccessException("Chỉ vai trò 'SecretaryDirector' hoặc 'Admin' mới có quyền phát hành chính thức công văn.");

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

        doc.Status = newStatus;
        doc.UpdatedAt = DateTime.UtcNow;

        if (newStatus == DocumentStatusConstants.Distributed)
        {
            doc.DistributedAt = DateTime.UtcNow;

            // Fix #6: Lookup email Thư ký BGD từ auth-service thay vì email cứng
            var secretaryDirector = await _authClient.GetUserByRoleAsync("SecretaryDirector");
            var recipientEmail = secretaryDirector?.Email
                ?? throw new InvalidOperationException(
                    "[auth-service] Không tìm thấy người dùng với vai trò 'SecretaryDirector' để gửi thông báo. " +
                    "Vui lòng đảm bảo auth-service đang chạy và đã có user với role này.");

            // Gọi notification-service và chờ kết quả — nếu service offline sẽ báo lỗi ngay
            await _notificationClient.SendNotificationAsync(new SendNotificationRequest(
                RecipientEmail: recipientEmail,
                Subject: $"[Công Văn Mới] {doc.DocumentNumber} - {doc.Title}",
                Body: $"Công văn số {doc.DocumentNumber} đã được phát hành chính thức."
            ));
        }

        _db.DocumentStatusHistory.Add(new DocumentStatusHistory
        {
            DocumentId = doc.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Note = req.Note?.Trim()
        });

        await _db.SaveChangesAsync();

        return (await GetByIdInternalAsync(doc.Id))!;
    }

    private async Task<Document?> GetByIdInternalAsync(Guid id)
    {
        return await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document> AssignAccessAsync(Guid id, AssignAccessRequest req, Guid userId, string? userRole)
    {
        // Fix #3: Kiểm tra quyền — chỉ Admin và SecretaryDirector được phân quyền phòng ban
        if (userRole != "Admin" && userRole != "SecretaryDirector")
            throw new UnauthorizedAccessException(
                $"Bạn không có quyền phân quyền phòng ban cho công văn. " +
                $"Chỉ vai trò 'Admin' hoặc 'SecretaryDirector' được phép thực hiện hành động này. " +
                $"Vai trò hiện tại của bạn: '{userRole ?? "không xác định"}'.");

        var doc = await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (req.DepartmentIds == null || req.DepartmentIds.Count == 0)
            throw new ArgumentException("Vui lòng chọn ít nhất một phòng ban để phân quyền.");

        foreach (var deptId in req.DepartmentIds)
        {
            if (!doc.DepartmentAccesses.Any(a => a.DepartmentId == deptId))
            {
                doc.DepartmentAccesses.Add(new DocumentDepartmentAccess
                {
                    DocumentId = doc.Id,
                    DepartmentId = deptId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = userId
                });
            }
        }

        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task<Document> AddAttachmentAsync(Guid id, AddAttachmentRequest req, Guid userId, string? userRole)
    {
        var doc = await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (doc.Status != DocumentStatusConstants.Draft)
            throw new InvalidOperationException("Chỉ có thể thêm file đính kèm khi công văn ở trạng thái 'Draft'.");

        bool isOwner = doc.CreatedByUserId == userId;
        bool isAuthorizedRole = userRole == "Admin" || userRole == "SecretaryDirector" || userRole == "Secretary";
        if (!isOwner && !isAuthorizedRole)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền thêm file đính kèm vào công văn này.");
        }

        if (req.FileId == Guid.Empty)
            throw new ArgumentException("FileId không hợp lệ.");

        // Validate files-service — ném KeyNotFoundException nếu file không tồn tại (404)
        var fileMeta = await _filesClient.GetFileByIdAsync(req.FileId);
        if (fileMeta == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy file đính kèm với ID: {req.FileId}");
        }

        var attachment = new DocumentAttachment
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            FileId = req.FileId,
            AttachmentType = req.AttachmentType ?? "Reference",
            CreatedAt = DateTime.UtcNow
        };
        _db.DocumentAttachments.Add(attachment);

        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return doc;
    }

    public async Task RemoveAttachmentAsync(Guid id, Guid attachmentId, Guid userId, string? userRole)
    {
        var doc = await _db.Documents
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (doc.Status != DocumentStatusConstants.Draft)
            throw new InvalidOperationException("Chỉ có thể xóa file đính kèm khi công văn ở trạng thái 'Draft'.");

        bool isOwner = doc.CreatedByUserId == userId;
        bool isAuthorizedRole = userRole == "Admin" || userRole == "SecretaryDirector" || userRole == "Secretary";
        if (!isOwner && !isAuthorizedRole)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xóa file đính kèm của công văn này.");
        }

        var attachment = doc.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment != null)
        {
            _db.DocumentAttachments.Remove(attachment);
            doc.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Document?> GetByIdAsync(Guid id, Guid? userDepartmentId, string? userRole)
    {
        var doc = await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null) return null;

        // ABAC Check: Admin, SecretaryDirector có quyền xem tất cả công văn
        if (userRole == "Admin" || userRole == "SecretaryDirector")
            return doc;

        if (userDepartmentId.HasValue)
        {
            bool hasAccess = doc.SenderDepartmentId == userDepartmentId.Value ||
                             doc.DepartmentAccesses.Any(a => a.DepartmentId == userDepartmentId.Value);

            if (!hasAccess && doc.Status != DocumentStatusConstants.Distributed)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập công văn này.");
        }
        else
        {
            // Fail-closed: Nếu không thuộc Admin/SecretaryDirector và token thiếu claim departmentId,
            // chỉ cho phép xem công văn đã được phát hành chính thức (Distributed).
            if (doc.Status != DocumentStatusConstants.Distributed)
                throw new UnauthorizedAccessException("Bạn không thuộc phòng ban nào và công văn này chưa được phát hành chính thức.");
        }

        return doc;
    }

    public async Task<PagedResult<Document>> GetListAsync(DocumentFilter filter, Guid? userDepartmentId, string? userRole)
    {
        var query = _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .AsQueryable();

        // Fail-closed RBAC/ABAC filtering cho người dùng không phải Admin / SecretaryDirector / Secretary
        if (userRole != "Admin" && userRole != "SecretaryDirector" && userRole != "Secretary")
        {
            if (userDepartmentId.HasValue)
            {
                var deptId = userDepartmentId.Value;
                query = query.Where(d => d.SenderDepartmentId == deptId ||
                                         d.DepartmentAccesses.Any(a => a.DepartmentId == deptId) ||
                                         d.Status == DocumentStatusConstants.Distributed);
            }
            else
            {
                // Fail-closed: User không có claim departmentId chỉ thấy công văn đã phát hành (Distributed)
                query = query.Where(d => d.Status == DocumentStatusConstants.Distributed);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(d => d.DocumentNumber.ToLower().Contains(term)
                                  || (d.ReferenceNumber != null && d.ReferenceNumber.ToLower().Contains(term))
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
            query = query.Where(d => d.SenderDepartmentId == filter.DepartmentId.Value
                                  || d.DepartmentAccesses.Any(a => a.DepartmentId == filter.DepartmentId.Value));
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

    public async Task<bool> DeleteAsync(Guid id, Guid userId, string? userRole)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return false;

        // Chỉ cho phép xóa công văn khi ở trạng thái Draft
        if (doc.Status != DocumentStatusConstants.Draft)
            throw new InvalidOperationException(
                $"Không thể xóa công văn khi ở trạng thái '{doc.Status}'. Chỉ công văn 'Draft' mới được phép xóa.");

        bool isOwner = doc.CreatedByUserId == userId;
        bool isAuthorizedRole = userRole == "Admin" || userRole == "SecretaryDirector" || userRole == "Secretary";
        if (!isOwner && !isAuthorizedRole)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xóa công văn này.");
        }

        // Soft Delete: đánh dấu xóa thay vì xóa cứng khỏi database
        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }
}
