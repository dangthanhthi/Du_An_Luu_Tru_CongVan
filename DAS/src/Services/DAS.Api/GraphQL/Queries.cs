using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate;
using DAS.Api.Models;
using DAS.Api.Services;

namespace DAS.Api.GraphQL
{
    public class DashboardStats
    {
        public int TotalIncoming { get; set; }
        public int TotalOutgoing { get; set; }
        public int TotalInternal { get; set; }
        public int PendingReview { get; set; }
    }

    public class Queries
    {
        public async Task<IEnumerable<User>> GetUsers([Service] IUserService userService) =>
            await userService.GetAllUsersAsync();

        public async Task<IEnumerable<Department>> GetDepartments([Service] IDepartmentService departmentService) =>
            await departmentService.GetAllDepartmentsAsync();

        public async Task<IEnumerable<ExternalEntity>> GetPartners([Service] IPartnerService partnerService) =>
            await partnerService.GetAllPartnersAsync();

        public async Task<IEnumerable<Document>> GetDocuments([Service] IDocumentService docService) =>
            await docService.GetAllDocumentsAsync();

        public async Task<IEnumerable<Document>> GetDocumentsByType(DocumentType type, [Service] IDocumentService docService) =>
            await docService.GetDocumentsByTypeAsync(type);

        public async Task<Document?> GetDocumentById(Guid id, [Service] IDocumentService docService) =>
            await docService.GetDocumentByIdAsync(id);

        public async Task<IEnumerable<Notification>> GetMyNotifications(Guid userId, [Service] INotificationService notiService) =>
            await notiService.GetUserNotificationsAsync(userId);

        public async Task<IEnumerable<DocumentComment>> GetDocumentComments(Guid docId, [Service] IDocumentService docService) =>
            await docService.GetCommentsAsync(docId);

        public async Task<DashboardStats> GetDashboardStats([Service] IDocumentService docService)
        {
            var docs = await docService.GetAllDocumentsAsync();
            return new DashboardStats
            {
                TotalIncoming = docs.Count(d => d.Type == DocumentType.Incoming),
                TotalOutgoing = docs.Count(d => d.Type == DocumentType.Outgoing),
                TotalInternal = docs.Count(d => d.Type == DocumentType.Internal),
                PendingReview = docs.Count(d => d.Status == DocumentStatus.PendingReview || d.Status == DocumentStatus.Draft)
            };
        }
    }
}
