# Database

`migrations/<service>/` chứa toàn bộ EF migrations và model snapshots của service đó. File chỉ có một bản tại đây; `backend/Directory.Build.props` đưa vào Compile của đúng project. Giữ nguyên migration ID, namespace và thứ tự lịch sử khi chuyển folder.

| Nhóm | Project sở hữu |
|---|---|
| `auth-service` | AuthService |
| `document-service` | DocumentService |
| `files-service` | FileService.API |
| `notification-service` | NotificationService |
| `partner-service` | PartnerService |
| `email-worker-service` | EmailWorkerService; baseline ba bảng IMAP/scan và snapshot SQL |

DbContext/entity nằm trong service để thể hiện ownership, cấu hình và validation. Không chia sẻ DbContext hoặc copy migration giữa service. EF tool phải tương thích lockfile: EmailWorker EF9.0.0; các store còn lại dùng tool10.0.3 (Notification runtime10.0.0, cùng major). Đặt migration **và snapshot** tại folder service ở đây; EF CLI có thể tạo snapshot trong folder `Migrations` của project khi chưa có snapshot liên kết, cần chuyển về canonical trước khi commit. Review SQL trước khi áp dụng vào môi trường được cho phép.

Xuất SQL idempotent và kiểm model/snapshot của cả sáu store, không kết nối DB:

```sh
python tools/export-database-schema.py --output .artifacts/qa/database-schema-01
```

Tool cài EF CLI cố định vào output riêng, restore locked, build Debug và gọi `has-pending-model-changes`, `migrations list --no-connect`, `migrations script --idempotent`. Output gồm sáu SQL script, migration IDs/hash và summary. Không áp dụng SQL, không tự nhập dữ liệu khách hàng. Xem [quy trình provision](PROVISIONING.md) và [SQL QA](../docs/SQL-QA.md).

`prisma/schema.prisma` là schema template auth frontend đã có, không phải database công văn chính. Output Prisma client trỏ về `frontend/node_modules/.prisma/client`.

Script seed và dữ liệu cũ đã được loại khỏi bản cộng tác. Khởi tạo schema theo migrations của đúng service trong môi trường được cho phép; không có bước tự tạo tài khoản demo hoặc nạp công văn cũ. DB thật/backup/PDF/credential không được đưa vào Git. Preflight/restore scripts chỉ chạy qua [tools](../tools/README.md) trên nguồn QA được chỉ định; migration khách hàng vẫn cần export/mapping/đối soát thật.
