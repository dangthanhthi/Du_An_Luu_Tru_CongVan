using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DAS.DocumentService.Data;
using DAS.DocumentService.DTOs;
using DAS.DocumentService.Entities;

namespace DAS.DocumentService.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly DocumentDbContext _context;
        private readonly IDocumentNumberGenerator _numberGenerator;

        public DocumentService(DocumentDbContext context, IDocumentNumberGenerator numberGenerator)
        {
            _context = context;
            _numberGenerator = numberGenerator;
        }

        public async Task<DocumentResponseDto> CreateIncomingDocumentAsync(CreateIncomingDocumentDto dto)
        {
            var docNumber = await _numberGenerator.GenerateDocumentNumberAsync("INCOMING");

            var entity = new DocumentEntity
            {
                Id = Guid.NewGuid(),
                DocumentNumber = docNumber,
                DocType = "INCOMING",
                Status = DocumentStatus.Draft.ToString(),
                Title = dto.Title,
                Summary = dto.Summary,
                PartnerId = dto.PartnerId,
                CreatedByUserId = dto.CreatedByUserId,
                ReceivedAt = dto.ReceivedAt ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.AttachmentFileIds != null && dto.AttachmentFileIds.Any())
            {
                foreach (var fileId in dto.AttachmentFileIds)
                {
                    entity.Attachments.Add(new DocumentAttachmentEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = entity.Id,
                        FileId = fileId,
                        AttachmentType = "Scan",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // Initial status audit log
            entity.StatusHistories.Add(new DocumentStatusHistoryEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = entity.Id,
                OldStatus = null,
                NewStatus = DocumentStatus.Draft.ToString(),
                ChangedByUserId = dto.CreatedByUserId,
                ChangedAt = DateTime.UtcNow,
                Note = "Khởi tạo công văn đến"
            });

            _context.Documents.Add(entity);
            await _context.SaveChangesAsync();

            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> CreateOutgoingDocumentAsync(CreateOutgoingDocumentDto dto)
        {
            var docNumber = await _numberGenerator.GenerateDocumentNumberAsync("OUTGOING");

            var entity = new DocumentEntity
            {
                Id = Guid.NewGuid(),
                DocumentNumber = docNumber,
                DocType = "OUTGOING",
                Status = DocumentStatus.Draft.ToString(),
                Title = dto.Title,
                Summary = dto.Summary,
                PartnerId = dto.PartnerId,
                SenderDepartmentId = dto.SenderDepartmentId,
                CreatedByUserId = dto.CreatedByUserId,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.AttachmentFileIds != null && dto.AttachmentFileIds.Any())
            {
                foreach (var fileId in dto.AttachmentFileIds)
                {
                    entity.Attachments.Add(new DocumentAttachmentEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = entity.Id,
                        FileId = fileId,
                        AttachmentType = "Original",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            entity.StatusHistories.Add(new DocumentStatusHistoryEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = entity.Id,
                OldStatus = null,
                NewStatus = DocumentStatus.Draft.ToString(),
                ChangedByUserId = dto.CreatedByUserId,
                ChangedAt = DateTime.UtcNow,
                Note = "Khởi tạo công văn đi"
            });

            _context.Documents.Add(entity);
            await _context.SaveChangesAsync();

            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> CreateInternalDocumentAsync(CreateInternalDocumentDto dto)
        {
            var docNumber = await _numberGenerator.GenerateDocumentNumberAsync("INTERNAL");

            var entity = new DocumentEntity
            {
                Id = Guid.NewGuid(),
                DocumentNumber = docNumber,
                DocType = "INTERNAL",
                Status = DocumentStatus.Draft.ToString(),
                Title = dto.Title,
                Summary = dto.Summary,
                SenderDepartmentId = dto.SenderDepartmentId,
                CreatedByUserId = dto.CreatedByUserId,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.AttachmentFileIds != null && dto.AttachmentFileIds.Any())
            {
                foreach (var fileId in dto.AttachmentFileIds)
                {
                    entity.Attachments.Add(new DocumentAttachmentEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = entity.Id,
                        FileId = fileId,
                        AttachmentType = "InternalReference",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            entity.StatusHistories.Add(new DocumentStatusHistoryEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = entity.Id,
                OldStatus = null,
                NewStatus = DocumentStatus.Draft.ToString(),
                ChangedByUserId = dto.CreatedByUserId,
                ChangedAt = DateTime.UtcNow,
                Note = "Khởi tạo công văn nội bộ"
            });

            _context.Documents.Add(entity);
            await _context.SaveChangesAsync();

            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> UpdateDocumentAsync(Guid id, UpdateDocumentDto dto)
        {
            var entity = await _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (entity == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy công văn với mã ID: {id}");
            }

            entity.Title = dto.Title;
            entity.Summary = dto.Summary;
            if (dto.PartnerId.HasValue) entity.PartnerId = dto.PartnerId;
            if (dto.SenderDepartmentId.HasValue) entity.SenderDepartmentId = dto.SenderDepartmentId;
            if (dto.ReceivedAt.HasValue) entity.ReceivedAt = dto.ReceivedAt;

            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> ChangeDocumentStatusAsync(Guid id, ChangeDocumentStatusDto dto)
        {
            var entity = await _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (entity == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy công văn với mã ID: {id}");
            }

            // Workflow state machine validation (Draft -> Reviewed -> Distributed)
            var oldStatus = entity.Status;
            var newStatus = dto.NewStatus;

            if (oldStatus == newStatus)
            {
                return MapToResponseDto(entity);
            }

            // Update status & timestamps
            entity.Status = newStatus;
            if (newStatus == DocumentStatus.Distributed.ToString())
            {
                entity.DistributedAt = DateTime.UtcNow;
            }
            entity.UpdatedAt = DateTime.UtcNow;

            // Audit Trail Record
            entity.StatusHistories.Add(new DocumentStatusHistoryEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = entity.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedByUserId = dto.ChangedByUserId,
                ChangedAt = DateTime.UtcNow,
                Note = dto.Note ?? $"Chuyển trạng thái từ {oldStatus} sang {newStatus}"
            });

            await _context.SaveChangesAsync();
            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> AssignDepartmentAccessAsync(Guid id, AssignDepartmentAccessDto dto)
        {
            var entity = await _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (entity == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy công văn với mã ID: {id}");
            }

            // Replace existing department access list with new assignment
            _context.DocumentDepartmentAccesses.RemoveRange(entity.DepartmentAccesses);

            if (dto.DepartmentIds != null && dto.DepartmentIds.Any())
            {
                foreach (var deptId in dto.DepartmentIds.Distinct())
                {
                    entity.DepartmentAccesses.Add(new DocumentDepartmentAccessEntity
                    {
                        DocumentId = entity.Id,
                        DepartmentId = deptId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedByUserId = dto.AssignedByUserId
                    });
                }
            }

            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> AddAttachmentAsync(Guid id, AddAttachmentDto dto)
        {
            var entity = await _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (entity == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy công văn với mã ID: {id}");
            }

            entity.Attachments.Add(new DocumentAttachmentEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = entity.Id,
                FileId = dto.FileId,
                AttachmentType = dto.AttachmentType ?? "Reference",
                CreatedAt = DateTime.UtcNow
            });

            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto> RemoveAttachmentAsync(Guid id, Guid attachmentId)
        {
            var entity = await _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (entity == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy công văn với mã ID: {id}");
            }

            var attachment = entity.Attachments.FirstOrDefault(a => a.Id == attachmentId);
            if (attachment != null)
            {
                _context.DocumentAttachments.Remove(attachment);
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return MapToResponseDto(entity);
        }

        public async Task<DocumentResponseDto?> GetDocumentByIdAsync(Guid id)
        {
            var entity = await _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .FirstOrDefaultAsync(d => d.Id == id);

            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<PagedResultDto<DocumentResponseDto>> GetDocumentsAsync(DocumentFilterDto filter)
        {
            var query = _context.Documents
                .Include(d => d.Attachments)
                .Include(d => d.DepartmentAccesses)
                .Include(d => d.StatusHistories)
                .AsQueryable();

            // Filters
            if (!string.IsNullOrWhiteSpace(filter.DocType))
            {
                query = query.Where(d => d.DocType.ToUpper() == filter.DocType.ToUpper());
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(d => d.Status == filter.Status);
            }

            if (filter.PartnerId.HasValue)
            {
                query = query.Where(d => d.PartnerId == filter.PartnerId.Value);
            }

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(d => d.SenderDepartmentId == filter.DepartmentId.Value ||
                                         d.DepartmentAccesses.Any(da => da.DepartmentId == filter.DepartmentId.Value));
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(d => d.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(d => d.CreatedAt <= filter.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(d => d.DocumentNumber.ToLower().Contains(term) ||
                                         d.Title.ToLower().Contains(term) ||
                                         (d.Summary != null && d.Summary.ToLower().Contains(term)));
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new PagedResultDto<DocumentResponseDto>
            {
                Items = items.Select(MapToResponseDto).ToList(),
                TotalItems = totalItems,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<bool> DeleteDocumentAsync(Guid id)
        {
            var entity = await _context.Documents.FindAsync(id);
            if (entity == null) return false;

            _context.Documents.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        private static DocumentResponseDto MapToResponseDto(DocumentEntity entity)
        {
            return new DocumentResponseDto
            {
                Id = entity.Id,
                DocumentNumber = entity.DocumentNumber,
                DocType = entity.DocType,
                Status = entity.Status,
                Title = entity.Title,
                Summary = entity.Summary,
                PartnerId = entity.PartnerId,
                SenderDepartmentId = entity.SenderDepartmentId,
                CreatedByUserId = entity.CreatedByUserId,
                ReceivedAt = entity.ReceivedAt,
                DistributedAt = entity.DistributedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Attachments = entity.Attachments.Select(a => new DocumentAttachmentDto
                {
                    Id = a.Id,
                    FileId = a.FileId,
                    AttachmentType = a.AttachmentType,
                    CreatedAt = a.CreatedAt
                }).ToList(),
                DepartmentAccesses = entity.DepartmentAccesses.Select(da => new DocumentDepartmentAccessDto
                {
                    DepartmentId = da.DepartmentId,
                    AssignedAt = da.AssignedAt,
                    AssignedByUserId = da.AssignedByUserId
                }).ToList(),
                StatusHistories = entity.StatusHistories.Select(sh => new DocumentStatusHistoryDto
                {
                    Id = sh.Id,
                    OldStatus = sh.OldStatus,
                    NewStatus = sh.NewStatus,
                    ChangedByUserId = sh.ChangedByUserId,
                    ChangedAt = sh.ChangedAt,
                    Note = sh.Note
                }).ToList()
            };
        }
    }
}
