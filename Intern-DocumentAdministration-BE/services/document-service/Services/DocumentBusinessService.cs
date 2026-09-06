using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DocumentService;

public class DocumentBusinessService : IDocumentBusinessService
{
    private readonly DocumentDbContext _db;
    private readonly INotificationServiceClient _notificationClient;

    public DocumentBusinessService(
        DocumentDbContext db,
        INotificationServiceClient notificationClient)
    {
        _db = db;
        _notificationClient = notificationClient;
    }

    private async Task<string> GenerateDocumentNumberAsync(string docType, string? title = null, string? customType = null, string? deptCode = null)
    {
        var year = DateTime.UtcNow.Year;
        int nextValue = 1;

        try
        {
            var executionStrategy = _db.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var counter = await _db.DocumentNumberCounters
                        .FromSqlRaw("SELECT * FROM document.DocumentNumberCounters WITH (UPDLOCK, HOLDLOCK) WHERE DocType = {0} AND Year = {1}", docType, year)
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
        catch
        {
            // Standard LINQ query fallback for local testing / non-MSSQL environments
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

        // Tự động suy luận loại văn bản viết tắt từ Tiêu đề hoặc loại truyền vào (QĐ, TB, BC, KH, CT, TTr, GM, NQ, HĐ, BB, QC, HD...)
        string typeShort = InferDocumentTypeShortCode(customType, title);
        string cleanDept = string.IsNullOrWhiteSpace(deptCode) ? "VP" : deptCode.Trim().ToUpperInvariant();

        return docType switch
        {
            // Văn bản Đến: Số đến trong Sổ đăng ký văn bản đến theo năm (Nghị định 30)
            DocumentTypeConstants.INCOMING => $"{nextValue:D4}/{year}",

            // Văn bản Đi: {Số}/{LoạiTắt}-{CơQuan/ĐơnVị} (Nghị định 30)
            DocumentTypeConstants.OUTGOING => $"{nextValue:D2}/{typeShort}-{cleanDept}",

            // Văn bản Nội bộ: {Số}/{LoạiTắt}-NB-{PhòngBan} (Nghị định 30)
            DocumentTypeConstants.INTERNAL => $"{nextValue:D2}/{typeShort}-NB-{cleanDept}",

            _ => $"{nextValue:D4}/{year}"
        };
    }

    private static string InferDocumentTypeShortCode(string? customType, string? title)
    {
        var text = (customType + " " + title).ToUpperInvariant();
        if (text.Contains("QUYẾT ĐỊNH") || text.Contains("QUYET DINH") || text.Contains("/QĐ")) return "QĐ";
        if (text.Contains("THÔNG BÁO") || text.Contains("THONG BAO") || text.Contains("/TB")) return "TB";
        if (text.Contains("BÁO CÁO") || text.Contains("BAO CAO") || text.Contains("/BC")) return "BC";
        if (text.Contains("TỜ TRÌNH") || text.Contains("TO TRINH") || text.Contains("/TTR")) return "TTr";
        if (text.Contains("KẾ HOẠCH") || text.Contains("KE HOACH") || text.Contains("/KH")) return "KH";
        if (text.Contains("CHỈ THỊ") || text.Contains("CHI THI") || text.Contains("/CT")) return "CT";
        if (text.Contains("GIẤY MỜI") || text.Contains("GIAY MOI") || text.Contains("/GM")) return "GM";
        if (text.Contains("NGHỊ QUYẾT") || text.Contains("NGHI QUYET") || text.Contains("/NQ")) return "NQ";
        if (text.Contains("HỢP ĐỒNG") || text.Contains("HOP DONG") || text.Contains("/HĐ")) return "HĐ";
        if (text.Contains("BIÊN BẢN") || text.Contains("BIEN BAN") || text.Contains("/BB")) return "BB";
        if (text.Contains("QUY CHẾ") || text.Contains("QUY CHE") || text.Contains("/QC")) return "QC";
        if (text.Contains("QUY ĐỊNH") || text.Contains("QUY DINH")) return "QĐ";
        if (text.Contains("HƯỚNG DẪN") || text.Contains("HUONG DAN") || text.Contains("/HD")) return "HD";
        if (text.Contains("CÔNG ĐIỆN") || text.Contains("CONG DIEN") || text.Contains("/CĐ")) return "CĐ";
        
        return "CV"; // Công văn
    }

    public async Task<Document> CreateIncomingAsync(CreateIncomingDocumentRequest req, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        var docNumber = await GenerateDocumentNumberAsync(DocumentTypeConstants.INCOMING, req.Title);

        var doc = new Document
        {
            DocumentNumber = docNumber,
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

        // Gửi thông báo thực tế khi tiếp nhận công văn mới
        _ = _notificationClient.SendNotificationAsync(new SendNotificationRequest(
            RecipientEmail: "vanthu@company.com",
            Subject: $"[Tiếp nhận Công văn đến] {doc.DocumentNumber} - {doc.Title}",
            Body: $"Hệ thống đã tiếp nhận công văn đến số {doc.DocumentNumber}: {doc.Title}. Vui lòng kiểm tra và xử lý."
        ));

        return doc;
    }

    public async Task<Document> CreateOutgoingAsync(CreateOutgoingDocumentRequest req, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        var docNumber = await GenerateDocumentNumberAsync(DocumentTypeConstants.OUTGOING, req.Title);

        var partnerId = req.PartnerId.HasValue && req.PartnerId.Value != Guid.Empty ? req.PartnerId.Value : (Guid?)null;
        var senderDeptId = req.SenderDepartmentId.HasValue && req.SenderDepartmentId.Value != Guid.Empty ? req.SenderDepartmentId.Value : (Guid?)null;

        var doc = new Document
        {
            DocumentNumber = docNumber,
            DocType = DocumentTypeConstants.OUTGOING,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = partnerId,
            SenderDepartmentId = senderDeptId,
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

        // Gửi thông báo thực tế khi tạo công văn đi
        _ = _notificationClient.SendNotificationAsync(new SendNotificationRequest(
            RecipientEmail: "vanthu@company.com",
            Subject: $"[Soạn thảo Công văn đi] {doc.DocumentNumber} - {doc.Title}",
            Body: $"Công văn đi số {doc.DocumentNumber} đã được khởi tạo và đang chờ duyệt."
        ));

        return doc;
    }

    public async Task<Document> CreateInternalAsync(CreateInternalDocumentRequest req, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        var docNumber = await GenerateDocumentNumberAsync(DocumentTypeConstants.INTERNAL, req.Title);
        var senderDeptId = req.SenderDepartmentId.HasValue && req.SenderDepartmentId.Value != Guid.Empty ? req.SenderDepartmentId.Value : (Guid?)null;

        var doc = new Document
        {
            DocumentNumber = docNumber,
            DocType = DocumentTypeConstants.INTERNAL,
            Status = DocumentStatusConstants.Draft,
            Title = req.Title.Trim(),
            Summary = req.Summary?.Trim(),
            PartnerId = null,
            SenderDepartmentId = senderDeptId,
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

        // Gửi thông báo thực tế khi tạo văn bản nội bộ
        _ = _notificationClient.SendNotificationAsync(new SendNotificationRequest(
            RecipientEmail: "all-staff@company.com",
            Subject: $"[Văn bản nội bộ mới] {doc.DocumentNumber} - {doc.Title}",
            Body: $"Văn bản nội bộ số {doc.DocumentNumber} ({doc.Title}) đã được tạo trên hệ thống."
        ));

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

        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ArgumentException("Tiêu đề công văn không được để trống.");

        doc.Title = req.Title.Trim();
        doc.Summary = req.Summary?.Trim();
        if (req.PartnerId.HasValue) doc.PartnerId = req.PartnerId.Value;
        if (req.SenderDepartmentId.HasValue) doc.SenderDepartmentId = req.SenderDepartmentId.Value;
        if (req.ReceivedAt.HasValue) doc.ReceivedAt = req.ReceivedAt.Value;
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<Document> ChangeStatusAsync(Guid id, ChangeStatusRequest req, Guid userId, string? userRole)
    {
        var oldStatus = await _db.Documents
            .Where(d => d.Id == id)
            .Select(d => (string?)d.Status)
            .FirstOrDefaultAsync();

        if (oldStatus == null)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        var newStatus = req.Status;

        if (oldStatus == newStatus)
            return await GetByIdWithDetailsAsync(id) ?? throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

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

        var now = DateTime.UtcNow;

        if (newStatus == DocumentStatusConstants.Distributed)
        {
            await _db.Documents
                .Where(d => d.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, newStatus)
                    .SetProperty(d => d.UpdatedAt, now)
                    .SetProperty(d => d.DistributedAt, now));
        }
        else
        {
            await _db.Documents
                .Where(d => d.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, newStatus)
                    .SetProperty(d => d.UpdatedAt, now));
        }

        _db.ChangeTracker.Clear();

        _db.DocumentStatusHistory.Add(new DocumentStatusHistory
        {
            DocumentId = id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = userId,
            ChangedAt = now,
            Note = req.Note?.Trim()
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
        }

        if (newStatus == DocumentStatusConstants.Distributed)
        {
            var doc = await GetByIdWithDetailsAsync(id);
            // Asynchronously notify via NotificationService (non-blocking)
            _ = _notificationClient.SendNotificationAsync(new SendNotificationRequest(
                RecipientEmail: "all-departments@company.com",
                Subject: $"[Công Văn Mới] {doc?.DocumentNumber} - {doc?.Title}",
                Body: $"Công văn số {doc?.DocumentNumber} đã được phát hành chính thức."
            ));
            return doc!;
        }

        return await GetByIdWithDetailsAsync(id) ?? throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");
    }

    private async Task<Document?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document> AssignAccessAsync(Guid id, AssignAccessRequest req, Guid userId)
    {
        var docExists = await _db.Documents.AnyAsync(d => d.Id == id);

        if (!docExists)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (req.DepartmentIds == null || req.DepartmentIds.Count == 0)
            throw new ArgumentException("Vui lòng chọn ít nhất một phòng ban để phân quyền.");

        var existingDeptIds = await _db.DocumentDepartmentAccess
            .Where(a => a.DocumentId == id)
            .Select(a => a.DepartmentId)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var deptId in req.DepartmentIds)
        {
            if (!existingDeptIds.Contains(deptId))
            {
                _db.DocumentDepartmentAccess.Add(new DocumentDepartmentAccess
                {
                    DocumentId = id,
                    DepartmentId = deptId,
                    AssignedAt = now,
                    AssignedByUserId = userId
                });
            }
        }

        await _db.SaveChangesAsync();

        await _db.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UpdatedAt, now));

        return await GetByIdWithDetailsAsync(id) ?? throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");
    }

    public async Task<Document> AddAttachmentAsync(Guid id, AddAttachmentRequest req)
    {
        var docExists = await _db.Documents.AnyAsync(d => d.Id == id);

        if (!docExists)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        if (req.FileId == Guid.Empty)
            throw new ArgumentException("FileId không hợp lệ.");

        // Insert trực tiếp DocumentAttachment thay vì load cả Document rồi sửa qua
        // navigation property + SaveChangesAsync — cách cũ khiến EF Core sinh câu
        // UPDATE document.Documents theo Id nhưng bị báo "0 rows affected" dù bản
        // ghi tồn tại (lỗi concurrency giả — không có concurrency token nào được
        // cấu hình trên Document). Insert thẳng + ExecuteUpdate né hoàn toàn lỗi này.
        _db.DocumentAttachments.Add(new DocumentAttachment
        {
            DocumentId = id,
            FileId = req.FileId,
            AttachmentType = req.AttachmentType ?? "Reference",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _db.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UpdatedAt, DateTime.UtcNow));

        var doc = await _db.Documents
            .Include(d => d.Attachments)
            .Include(d => d.DepartmentAccesses)
            .Include(d => d.StatusHistories)
            .FirstAsync(d => d.Id == id);

        return doc;
    }

    public async Task RemoveAttachmentAsync(Guid id, Guid attachmentId)
    {
        var docExists = await _db.Documents.AnyAsync(d => d.Id == id);

        if (!docExists)
            throw new KeyNotFoundException($"Không tìm thấy công văn với ID: {id}");

        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == id);

        if (attachment != null)
        {
            _db.DocumentAttachments.Remove(attachment);
            await _db.SaveChangesAsync();

            await _db.Documents
                .Where(d => d.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.UpdatedAt, DateTime.UtcNow));
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

        // ABAC Check: Admin, SecretaryDirector, or creator/sender or assigned department
        if (userRole == "Admin" || userRole == "SecretaryDirector")
            return doc;

        if (userDepartmentId.HasValue)
        {
            bool hasAccess = doc.SenderDepartmentId == userDepartmentId.Value ||
                             doc.DepartmentAccesses.Any(a => a.DepartmentId == userDepartmentId.Value);

            if (!hasAccess && doc.Status != DocumentStatusConstants.Distributed)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập công văn này.");
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

        // RBAC/ABAC filtering for non-admin/non-secretary-director users
        if (userRole != "Admin" && userRole != "SecretaryDirector" && userDepartmentId.HasValue)
        {
            var deptId = userDepartmentId.Value;
            query = query.Where(d => d.SenderDepartmentId == deptId ||
                                     d.DepartmentAccesses.Any(a => a.DepartmentId == deptId) ||
                                     d.Status == DocumentStatusConstants.Distributed);
        }

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

    public async Task<bool> DeleteAsync(Guid id, string? userRole)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return false;

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
}