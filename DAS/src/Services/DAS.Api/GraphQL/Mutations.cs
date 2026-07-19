using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate;
using DAS.Api.Models;
using DAS.Api.Services;

namespace DAS.Api.GraphQL
{
    public class LoginPayload
    {
        public User User { get; set; } = null!;
        public string Token { get; set; } = string.Empty;
    }

    public class Mutations
    {
        public async Task<User> RegisterUser(
            string email,
            string name,
            string password,
            Role role,
            Guid? departmentId,
            [Service] IUserService userService) =>
            await userService.RegisterUserAsync(email, name, password, role, departmentId);

        public async Task<LoginPayload?> Login(
            string email,
            string password,
            [Service] IUserService userService)
        {
            var res = await userService.LoginAsync(email, password);
            if (res == null) return null;
            return new LoginPayload { User = res.Value.User, Token = res.Value.Token };
        }

        public async Task<Document> CreateDocument(
            string subject,
            string content,
            DocumentType type,
            DocumentPriority priority,
            Guid? externalEntityId,
            Guid createdByUserId,
            string? originalDocNumber,
            string? fileName,
            string? filePath,
            long? fileSize,
            [Service] IDocumentService docService)
        {
            var doc = new Document
            {
                Subject = subject,
                Content = content,
                Type = type,
                Priority = priority,
                ExternalEntityId = externalEntityId,
                CreatedByUserId = createdByUserId,
                OriginalDocNumber = originalDocNumber,
                Status = DocumentStatus.Draft
            };
            return await docService.CreateDocumentAsync(doc, fileName, filePath, fileSize);
        }

        public async Task<Document> DistributeDocument(
            Guid docId,
            List<Guid> departmentIds,
            string note,
            [Service] IDocumentService docService) =>
            await docService.DistributeDocumentAsync(docId, departmentIds, note);

        public async Task<Document> UpdateDocumentStatus(
            Guid docId,
            DocumentStatus status,
            [Service] IDocumentService docService) =>
            await docService.UpdateStatusAsync(docId, status);

        public async Task<DocumentComment> AddDocumentComment(
            Guid docId,
            Guid userId,
            string content,
            [Service] IDocumentService docService) =>
            await docService.AddCommentAsync(docId, userId, content);

        public async Task<ExternalEntity> CreatePartner(
            string fullName,
            string shortName,
            string taxCode,
            string address,
            string contactPerson,
            string phone,
            string email,
            PartnerType type,
            [Service] IPartnerService partnerService) =>
            await partnerService.CreatePartnerAsync(new ExternalEntity
            {
                FullName = fullName,
                ShortName = shortName,
                TaxCode = taxCode,
                Address = address,
                ContactPerson = contactPerson,
                Phone = phone,
                Email = email,
                Type = type
            });

        public async Task<ExternalEntity> UpdatePartner(
            Guid id,
            string fullName,
            string shortName,
            string taxCode,
            string address,
            string contactPerson,
            string phone,
            string email,
            PartnerType type,
            [Service] IPartnerService partnerService)
        {
            var partner = await partnerService.GetPartnerByIdAsync(id);
            if (partner == null) throw new Exception("Không tìm thấy đối tác.");
            partner.FullName = fullName;
            partner.ShortName = shortName;
            partner.TaxCode = taxCode;
            partner.Address = address;
            partner.ContactPerson = contactPerson;
            partner.Phone = phone;
            partner.Email = email;
            partner.Type = type;
            return await partnerService.UpdatePartnerAsync(partner);
        }

        public async Task<bool> DeletePartner(Guid id, [Service] IPartnerService partnerService) =>
            await partnerService.DeletePartnerAsync(id);

        public async Task<Department> CreateDepartment(
            string name,
            string code,
            Guid? parentId,
            [Service] IDepartmentService deptService) =>
            await deptService.CreateDepartmentAsync(new Department
            {
                Name = name,
                Code = code,
                ParentId = parentId
            });

        public async Task<bool> MarkNotificationRead(Guid id, [Service] INotificationService notiService) =>
            await notiService.MarkAsReadAsync(id);
    }
}
