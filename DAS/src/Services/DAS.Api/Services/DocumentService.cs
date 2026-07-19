using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DAS.Api.Data;
using DAS.Api.Models;

namespace DAS.Api.Services
{
    public interface IDocumentService
    {
        Task<Document> CreateDocumentAsync(Document doc, string? fileName, string? filePath, long? fileSize);
        Task<Document?> GetDocumentByIdAsync(Guid id);
        Task<IEnumerable<Document>> GetDocumentsByTypeAsync(DocumentType type);
        Task<IEnumerable<Document>> GetAllDocumentsAsync();
        Task<Document> DistributeDocumentAsync(Guid docId, List<Guid> departmentIds, string note);
        Task<Document> UpdateStatusAsync(Guid docId, DocumentStatus status);
        Task<DocumentComment> AddCommentAsync(Guid docId, Guid userId, string content);
        Task<IEnumerable<DocumentComment>> GetCommentsAsync(Guid docId);
        Task<IEnumerable<DocumentDepartment>> GetDistributionAsync(Guid docId);
    }

    public class DocumentService : IDocumentService
    {
        private readonly AppDbContext _context;

        public DocumentService(AppDbContext context)
        {
            _context = context;
        }

        private async Task<string> GenerateDocumentNumberAsync(DocumentType type)
        {
            string prefix = type switch
            {
                DocumentType.Incoming => "CV-DEN",
                DocumentType.Outgoing => "CV-DI",
                DocumentType.Internal => "CV-NB",
                _ => "CV"
            };

            int year = DateTime.UtcNow.Year;
            
            // Count existing documents of this type in this year
            int count = await _context.Documents
                .Where(d => d.Type == type && d.CreatedAt.Year == year)
                .CountAsync();

            return $"{prefix}-{year}-{(count + 1).ToString("D5")}";
        }

        public async Task<Document> CreateDocumentAsync(Document doc, string? fileName, string? filePath, long? fileSize)
        {
            doc.DocumentNumber = await GenerateDocumentNumberAsync(doc.Type);
            doc.CreatedAt = DateTime.UtcNow;
            doc.UpdatedAt = DateTime.UtcNow;

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(filePath))
            {
                var docFile = new DocumentFile
                {
                    DocumentId = doc.Id,
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize ?? 0,
                    MimeType = "application/pdf",
                    Version = 1,
                    UploadedAt = DateTime.UtcNow
                };
                _context.DocumentFiles.Add(docFile);
                await _context.SaveChangesAsync();
            }

            return doc;
        }

        public async Task<Document?> GetDocumentByIdAsync(Guid id)
        {
            return await _context.Documents.FindAsync(id);
        }

        public async Task<IEnumerable<Document>> GetDocumentsByTypeAsync(DocumentType type)
        {
            return await _context.Documents.Where(d => d.Type == type).ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetAllDocumentsAsync()
        {
            return await _context.Documents.ToListAsync();
        }

        public async Task<Document> DistributeDocumentAsync(Guid docId, List<Guid> departmentIds, string note)
        {
            var doc = await GetDocumentByIdAsync(docId);
            if (doc == null) throw new Exception("Không tìm thấy công văn.");

            // Remove existing distributions
            var existing = await _context.DocumentDepartments.Where(dd => dd.DocumentId == docId).ToListAsync();
            _context.DocumentDepartments.RemoveRange(existing);

            foreach (var deptId in departmentIds)
            {
                var dist = new DocumentDepartment
                {
                    DocumentId = docId,
                    DepartmentId = deptId,
                    ProcessingStatus = "Chờ xử lý",
                    Note = note,
                    AssignedAt = DateTime.UtcNow
                };
                _context.DocumentDepartments.Add(dist);
            }

            doc.Status = DocumentStatus.Distributed;
            doc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return doc;
        }

        public async Task<Document> UpdateStatusAsync(Guid docId, DocumentStatus status)
        {
            var doc = await GetDocumentByIdAsync(docId);
            if (doc == null) throw new Exception("Không tìm thấy công văn.");

            doc.Status = status;
            doc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return doc;
        }

        public async Task<DocumentComment> AddCommentAsync(Guid docId, Guid userId, string content)
        {
            var comment = new DocumentComment
            {
                DocumentId = docId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };
            _context.DocumentComments.Add(comment);
            await _context.SaveChangesAsync();
            return comment;
        }

        public async Task<IEnumerable<DocumentComment>> GetCommentsAsync(Guid docId)
        {
            return await _context.DocumentComments
                .Where(c => c.DocumentId == docId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<DocumentDepartment>> GetDistributionAsync(Guid docId)
        {
            return await _context.DocumentDepartments.Where(dd => dd.DocumentId == docId).ToListAsync();
        }
    }
}
