using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentService;

namespace DocumentService;

public static class DemoDocumentSeed
{
    public static async Task SeedAsync(DocumentDbContext db)
    {
        if (await db.Documents.AnyAsync()) return;

        var adminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var hcDeptId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var bgdDeptId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var doc1 = new Document
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "01/CV-BGD/2026",
            DocType = DocumentTypeConstants.INCOMING,
            Status = "InProgress",
            Title = "Công văn số 01: V/v Triển khai công tác lưu trữ số và bảo mật dữ liệu công văn",
            Summary = "Hướng dẫn chi tiết quy trình tiếp nhận, phân loại và lưu trữ văn bản điện tử năm 2026.",
            CreatedByUserId = secUserId,
            SenderDepartmentId = bgdDeptId,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ReceivedAt = DateTime.UtcNow.AddDays(-2),
            IsDeleted = false
        };

        doc1.Registration = new DocumentRegistration
        {
            DocumentId = doc1.Id,
            Kind = "INCOMING",
            Version = 1,
            RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            RegistrationYear = 2026,
            SequenceNumber = 1,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-2),
            CompanyCode = "HL",
            CompanyNameSnapshot = "Hoàng Long",
            OwnerDepartmentId = hcDeptId,
            OwnerDepartmentCodeSnapshot = "HC-VT",
            OwnerDepartmentNameSnapshot = "Phòng Hành chính - Văn thư",
            InputterUserId = secUserId,
            OriginatorUserId = secUserId,
            LastModifierUserId = secUserId,
            Sensitivity = "Normal",
            Remark = "Công văn tiếp nhận mẫu phục vụ kiểm thử"
        };

        doc1.KindDetails = new DocumentKindDetails
        {
            DocumentId = doc1.Id,
            ReceivingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            ReferenceNumber = "01/2026/CV-UBND",
            MethodCode = "EMAIL",
            MethodNameSnapshot = "eMail",
            DocumentTypeCode = "LETTER",
            DocumentTypeNameSnapshot = "Letter"
        };

        doc1.DepartmentAccesses.Add(new DocumentDepartmentAccess
        {
            DocumentId = doc1.Id,
            DepartmentId = hcDeptId,
            AssignedByUserId = adminUserId,
            AssignedAt = DateTime.UtcNow.AddDays(-2)
        });

        var doc2 = new Document
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "02/TB-HCVT/2026",
            DocType = DocumentTypeConstants.OUTGOING,
            Status = "Distributed",
            Title = "Thông báo số 02: Kế hoạch rà soát văn thư định kỳ Quý 4/2026",
            Summary = "Thông báo lịch làm việc và bàn giao sổ lưu trữ công văn đến các phòng ban.",
            CreatedByUserId = adminUserId,
            SenderDepartmentId = hcDeptId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            DistributedAt = DateTime.UtcNow.AddDays(-4),
            IsDeleted = false
        };

        doc2.Registration = new DocumentRegistration
        {
            DocumentId = doc2.Id,
            Kind = "OUTGOING",
            Version = 1,
            RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            RegistrationYear = 2026,
            SequenceNumber = 2,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-5),
            CompanyCode = "HV",
            CompanyNameSnapshot = "Hoàn Vũ",
            OwnerDepartmentId = hcDeptId,
            OwnerDepartmentCodeSnapshot = "HC-VT",
            OwnerDepartmentNameSnapshot = "Phòng Hành chính - Văn thư",
            InputterUserId = adminUserId,
            OriginatorUserId = adminUserId,
            LastModifierUserId = adminUserId,
            Sensitivity = "Normal",
            Remark = "Đã phát hành"
        };

        doc2.KindDetails = new DocumentKindDetails
        {
            DocumentId = doc2.Id,
            ReferenceNumber = "02/TB-DAS",
            MethodCode = "FAX",
            MethodNameSnapshot = "Fax",
            DocumentTypeCode = "NOTIFICATION",
            DocumentTypeNameSnapshot = "Notification"
        };

        doc2.DepartmentAccesses.Add(new DocumentDepartmentAccess
        {
            DocumentId = doc2.Id,
            DepartmentId = hcDeptId,
            AssignedByUserId = adminUserId,
            AssignedAt = DateTime.UtcNow.AddDays(-5)
        });

        var doc3 = new Document
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "03/BB-KTCN/2026",
            DocType = DocumentTypeConstants.INTERNAL,
            Status = "InProgress",
            Title = "Tờ trình số 03: Đề xuất nâng cấp hạ tầng máy chủ lưu trữ điện tử",
            Summary = "Báo cáo năng lực hệ thống hiện tại và đề xuất mở rộng dung lượng ổ cứng.",
            CreatedByUserId = secUserId,
            SenderDepartmentId = hcDeptId,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false
        };

        doc3.Registration = new DocumentRegistration
        {
            DocumentId = doc3.Id,
            Kind = "INTERNAL",
            Version = 1,
            RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RegistrationYear = 2026,
            SequenceNumber = 3,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1),
            CompanyCode = "HLHV",
            CompanyNameSnapshot = "Hoàng Long Hoàn Vũ",
            OwnerDepartmentId = hcDeptId,
            OwnerDepartmentCodeSnapshot = "HC-VT",
            OwnerDepartmentNameSnapshot = "Phòng Hành chính - Văn thư",
            InputterUserId = secUserId,
            OriginatorUserId = secUserId,
            LastModifierUserId = secUserId,
            Sensitivity = "Confidential",
            Remark = "Đang xử lý nội bộ"
        };

        doc3.KindDetails = new DocumentKindDetails
        {
            DocumentId = doc3.Id,
            DocumentTypeCode = "STATEMENT",
            DocumentTypeNameSnapshot = "Statement"
        };

        doc3.DepartmentAccesses.Add(new DocumentDepartmentAccess
        {
            DocumentId = doc3.Id,
            DepartmentId = hcDeptId,
            AssignedByUserId = secUserId,
            AssignedAt = DateTime.UtcNow.AddDays(-1)
        });

        db.Documents.AddRange(doc1, doc2, doc3);
        await db.SaveChangesAsync();
    }
}
