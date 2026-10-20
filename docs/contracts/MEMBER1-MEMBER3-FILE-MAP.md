# Bản đồ file ghép backend thành viên 1 và 3

Ngày 09/10/2026. Đây là bảng nguồn local, không phải danh sách file đã merge/push. Phương án và DI: [đọc tại đây](MEMBER1-MEMBER3-INTEGRATION-PLAN.md).

Có 120 file trong phạm vi runtime/source build của bốn service và PdfProtocol. Auth/EAP, Email/OCR, design-time factories, migrations, cấu hình local, seeds/dữ liệu, tests và output không nằm trong bảng chuyển này.

Hành động REPLACE/MOVE_ONLY được đối chiếu từng file với main cũ 36f8f0f240214f2d50a93cc86bee34d880d3c250. Đây là so sánh nội dung file sau chuẩn hóa xuống dòng, không phải khẳng định khác biệt wire contract cho tất cả file thay thế.

Target luôn bằng đường dẫn nguồn local. Old path là nơi file tương ứng đã tồn tại trong layout cũ; “—” nghĩa là chưa có file tại vị trí tương ứng. Nhóm trong bảng là phạm vi sở hữu source; workflow/startup chưa đại diện cho một nhánh Git mới được tạo.

## Bước 0 — 6 file

| Nguồn local = đích | Main cũ | Hành động | Nhóm | Ghi chú |
|---|---|---|---|---|
| `backend/Directory.Build.props` | — | ADD_BUILD_RULES | architecture |  |
| `backend/services/document-service/DocumentService.csproj` | `services/document-service/DocumentService.csproj` | MERGE_PROJECT | architecture |  |
| `backend/services/files-service/FileService.API.csproj` | `services/files-service/FileService.API.csproj` | MERGE_PROJECT | architecture |  |
| `backend/services/notification-service/NotificationService.csproj` | `services/notification-service/NotificationService.csproj` | MERGE_PROJECT | architecture |  |
| `backend/services/partner-service/PartnerService.csproj` | `services/partner-service/PartnerService.csproj` | MERGE_PROJECT | architecture |  |
| `backend/shared/PdfProtocol/PdfProtocol.csproj` | — | ADD | architecture |  |

## Bước 1 — 23 file

| Nguồn local = đích | Main cũ | Hành động | Nhóm | Ghi chú |
|---|---|---|---|---|
| `backend/services/document-service/Data/BusinessCatalogSeed.cs` | — | ADD | database |  |
| `backend/services/document-service/Data/DocumentDbContext.cs` | `services/document-service/Data/DocumentDbContext.cs` | REPLACE | database |  |
| `backend/services/document-service/Data/DocumentV2Model.cs` | — | ADD | database |  |
| `backend/services/document-service/Data/V2MutationLocks.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/BusinessCatalogs.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/CurrentPdf.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/DeliveryEntities.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/DocumentCancellation.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/DocumentEditAudit.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/DocumentKindDetails.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/DocumentRelation.cs` | — | ADD | database |  |
| `backend/services/document-service/Models/Entities/Documents.cs` | `services/document-service/Models/Entities/Documents.cs` | REPLACE | database |  |
| `backend/services/document-service/Models/Entities/DocumentV2Persistence.cs` | — | ADD | database |  |
| `backend/services/files-service/Data/FileDbContext.cs` | `services/files-service/Data/FileDbContext.cs` | REPLACE | database |  |
| `backend/services/files-service/Models/Entities/FileRecord.cs` | `services/files-service/Models/Entities/FileRecord.cs` | MOVE_ONLY | database |  |
| `backend/services/files-service/Models/Entities/PdfClaim.cs` | — | ADD | database |  |
| `backend/services/files-service/Models/Entities/PdfUpload.cs` | — | ADD | database |  |
| `backend/services/notification-service/Data/NotificationDbContext.cs` | `services/notification-service/Data/NotificationDbContext.cs` | REPLACE | database |  |
| `backend/services/notification-service/Models/DeliveryInbox.cs` | — | ADD | database |  |
| `backend/services/notification-service/Models/InAppNotification.cs` | — | ADD | database |  |
| `backend/services/notification-service/Models/NotificationLog.cs` | `services/notification-service/Models/NotificationLog.cs` | REPLACE | database |  |
| `backend/services/notification-service/Models/UserNotificationPreference.cs` | — | ADD | database |  |
| `backend/services/partner-service/Infrastructure/Persistence/PartnerDbContext.cs` | `services/partner-service/Infrastructure/Persistence/PartnerDbContext.cs` | REPLACE | database | Chỉ runtime entity/context; bỏ khối PartnerDbContextFactory ở cuối file khi chuyển (không chuyển connection factory). |

## Bước 2 — 14 file

| Nguồn local = đích | Main cũ | Hành động | Nhóm | Ghi chú |
|---|---|---|---|---|
| `backend/services/document-service/Authorization/DocumentActor.cs` | `services/document-service/Authorization/DocumentActor.cs` | REPLACE | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/DocumentContracts.cs` | `services/document-service/Models/DTOs/DocumentContracts.cs` | MOVE_ONLY | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/RegistrationContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/ServiceClientContracts.cs` | `services/document-service/Models/DTOs/ServiceClientContracts.cs` | REPLACE | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2EditingContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2HistoryContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2HttpContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2KindDetailsContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2ReadContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2RelationContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/document-service/Models/DTOs/V2StatusContracts.cs` | — | ADD | shared-contracts |  |
| `backend/services/files-service/Models/DTOs/ApiResponse.cs` | `services/files-service/Models/DTOs/ApiResponse.cs` | REPLACE | shared-contracts |  |
| `backend/services/files-service/Models/DTOs/ManagedFileInfo.cs` | — | ADD | shared-contracts |  |
| `backend/shared/PdfProtocol/Contracts.cs` | — | ADD | shared-contracts |  |

## Bước 3 — 71 file

| Nguồn local = đích | Main cũ | Hành động | Nhóm | Ghi chú |
|---|---|---|---|---|
| `backend/services/document-service/Authorization/DocumentAccessRules.cs` | `services/document-service/Authorization/DocumentAccessRules.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Common/GlobalExceptionHandler.cs` | `services/document-service/Common/GlobalExceptionHandler.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/CatalogsController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/CurrentPdfController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/DocumentHistoryController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/DocumentsController.cs` | `services/document-service/Controllers/DocumentsController.cs` | MOVE_ONLY | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/DocumentsV2Controller.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/DocumentTasksController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/IncompleteReportsController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/MyStaffController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/PdfProtocolController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Controllers/ReminderOperationsController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Data/DatabaseStartup.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Services/Authorization/DocumentV2Authority.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Services/CatalogAdminQuery.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Services/CatalogService.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Services/Integration/ForwardUserTokenHandler.cs` | `services/document-service/Services/Integration/ForwardUserTokenHandler.cs` | MOVE_ONLY | workflow chung TV1+TV3 |  |
| `backend/services/document-service/Services/Integration/InterServiceClients.cs` | `services/document-service/Services/Integration/InterServiceClients.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Controllers/FilesController.cs` | `services/files-service/Controllers/FilesController.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Controllers/PdfClaimsController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Data/FileDatabaseOptions.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Data/FileSqliteUpgrade.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/FileRuleException.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/FileStorageService.cs` | `services/files-service/Services/FileStorageService.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/IFileStorageService.cs` | `services/files-service/Services/IFileStorageService.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/PdfClaims.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/PdfMaintenance.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/PdfUploadMaintenance.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Services/PdfValidationService.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/files-service/Storage/PdfStorageOptions.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/notification-service/Controllers/NotificationsController.cs` | `services/notification-service/Controllers/NotificationsController.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/notification-service/Data/NotificationDatabaseOptions.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/notification-service/Hubs/NotificationHub.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/notification-service/Services/ConfiguredDeliveryEmail.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/notification-service/Services/EmailTemplateHelper.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/partner-service/Api/Errors/GlobalExceptionHandler.cs` | `services/partner-service/Api/Errors/GlobalExceptionHandler.cs` | MOVE_ONLY | workflow chung TV1+TV3 |  |
| `backend/services/partner-service/Application/PartnerAuditQuery.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/partner-service/Application/PartnerBusinessService.cs` | `services/partner-service/Application/PartnerBusinessService.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/partner-service/Controllers/PartnerHistoryController.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/services/partner-service/Controllers/PartnersController.cs` | `services/partner-service/Controllers/PartnersController.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `backend/services/partner-service/Infrastructure/Persistence/PartnerDatabaseOptions.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/shared/PdfProtocol/HttpClients.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/shared/PdfProtocol/InternalPdfKeyAttribute.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/shared/PdfProtocol/PdfMaintenanceWorker.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `backend/shared/PdfProtocol/PdfProtocolSettings.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Completion/DocumentCompletionEvaluator.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/DocumentBusinessService.cs` | `services/document-service/Services/DocumentBusinessService.cs` | REPLACE | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Editing/V2DistributionRules.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Editing/V2DocumentEditor.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Editing/V2DocumentLifecycle.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Editing/V2EditAuthority.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Editing/V2KindDetailsMapper.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Files/CurrentPdfService.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Files/PdfAuthority.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Files/PdfMaintenance.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Notifications/ConfiguredNotificationTransport.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Notifications/DocumentNotifications.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Queries/V2DocumentQueries.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Queries/V2LifecycleHistory.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Queries/V2LifecycleHistoryParser.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Registration/V2RegistrationService.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Relations/V2DocumentRelations.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Reminders/DurableReminderTransport.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Reminders/ReminderEligibility.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Reminders/WeeklyReminders.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Reports/IncompleteReports.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Reports/ReportWorkbook.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Tasks/DocumentTasks.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Tasks/MyStaff.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/document-service/Tasks/MyStaffTasksResult.cs` | — | ADD | workflow chung TV1+TV3 |  |
| `workflows/business/notification-service/DurableNotifications.cs` | — | ADD | workflow chung TV1+TV3 |  |

## Bước 4 — 2 file

| Nguồn local = đích | Main cũ | Hành động | Nhóm | Ghi chú |
|---|---|---|---|---|
| `workflows/business/document-service/Numbering/DocumentNumberFormatter.cs` | — | ADD | numbering |  |
| `workflows/business/document-service/Numbering/DocumentRegistrationWriter.cs` | — | ADD | numbering |  |

## Bước 5 — 4 file

| Nguồn local = đích | Main cũ | Hành động | Nhóm | Ghi chú |
|---|---|---|---|---|
| `backend/services/document-service/Program.cs` | `services/document-service/Program.cs` | REPLACE | service startup | Một Program cho mỗi host; ghép dependency và DI cùng phiên bản, bảo toàn cấu hình deployment riêng. |
| `backend/services/files-service/Program.cs` | `services/files-service/Program.cs` | REPLACE | service startup | Một Program cho mỗi host; ghép dependency và DI cùng phiên bản, bảo toàn cấu hình deployment riêng. |
| `backend/services/notification-service/Program.cs` | `services/notification-service/Program.cs` | REPLACE | service startup | Một Program cho mỗi host; ghép dependency và DI cùng phiên bản, bảo toàn cấu hình deployment riêng. |
| `backend/services/partner-service/Program.cs` | `services/partner-service/Program.cs` | REPLACE | service startup | Một Program cho mỗi host; ghép dependency và DI cùng phiên bản, bảo toàn cấu hình deployment riêng. |
