# Database

`migrations/<service>/` chứa toàn bộ EF migrations và model snapshots của service đó. File chỉ có một bản tại đây; `backend/Directory.Build.props` đưa vào Compile của đúng project. Giữ nguyên migration ID, namespace và thứ tự lịch sử khi chuyển folder.

| Nhóm | Project sở hữu |
|---|---|
| `auth-service` | AuthService |
| `document-service` | DocumentService |
| `files-service` | FileService.API |
| `notification-service` | NotificationService |
| `partner-service` | PartnerService |
| `email-worker-service` | EmailWorkerService; chỉ có migration nếu nguồn đã có |

DbContext/entity nằm trong service để thể hiện ownership, cấu hình và validation. Không chia sẻ DbContext hoặc copy migration giữa service. Để tạo migration mới, dùng EF tool tương thích10.0.3 và đúng project/context, đặt output vào folder service tại đây; review SQL trước khi áp dụng vào môi trường được cho phép.

`prisma/schema.prisma` là schema template auth frontend đã có, không phải database công văn chính. Output Prisma client trỏ về `frontend/node_modules/.prisma/client`.

Script seed và dữ liệu cũ đã được loại khỏi bản cộng tác. Khởi tạo schema theo migrations của đúng service trong môi trường được cho phép; không có bước tự tạo tài khoản demo hoặc nạp công văn cũ. DB thật/backup/PDF/credential không được đưa vào Git. Preflight/restore scripts chỉ chạy qua [tools](../tools/README.md) trên nguồn QA được chỉ định; migration khách hàng vẫn cần export/mapping/đối soát thật.
