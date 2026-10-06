# Workflow

Mã C# của luồng xử lý được nhóm theo service sở hữu, không phải một service mới hoặc một bản sao. Namespace và dependency injection giữ nguyên. MSBuild liên kết file vào project backend tương ứng.

| Folder trong `business/document-service/` | Luồng |
|---|---|
| `Registration`, `Numbering` | Đăng ký, cấp số theo loại/năm, idempotency |
| `Editing` | Sửa dữ liệu, phân phối, hủy và khôi phục |
| `Relations`, `Completion` | Liên kết công văn và đánh giá hồ sơ hoàn tất |
| `Files` | PDF hiện hành, claims và bảo trì giao thức |
| `Queries` | Danh sách/lọc theo phạm vi backend |
| `Reports`, `Reminders` | Báo cáo/XLSX và nhắc quá 14 ngày, thứ Hai 08:00 giờ Việt Nam |
| `Notifications` | Thông báo công văn và prepared transport |
| `Tasks` | My Staff và intent/retry/reconcile task |
| `DocumentBusinessService.cs` | Adapter nghiệp vụ công văn hiện có |

`business/notification-service/DurableNotifications.cs` chứa durable notification acceptance/dedup/delivery. HTTP/controller/DTO, resource policy và integration clients ở backend service; logic nghiệp vụ ở workflow dùng các contract đó.

CI khai báo ở `.github/workflows/` vì GitHub chỉ chạy workflow tại vị trí này. Công cụ QA/build ở `tools/`. Đây là các nhóm khác nhau, không trộn vào mã nghiệp vụ.

Không bật SMTP/TMS khi chưa có config/contract; Queued/Accepted không bằng Sent. EAP/OCR hoãn; Fax theo kế hoạch cuối. [Quy tắc nghiệp vụ](../docs/BUSINESS-RULES.md).
