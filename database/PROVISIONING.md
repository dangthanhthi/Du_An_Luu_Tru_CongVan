# Provision và bàn giao database DAS

Sáu service sở hữu sáu store riêng: auth, document, files, notification, partner, email-worker. Không dùng frontend Prisma như schema nghiệp vụ và không copy bảng/DbContext giữa service. Connection phải chỉ đúng store được bàn giao; không suy ra mọi service cùng dùng database từ cấu hình template cũ.

## Chuẩn bị schema để review

Chạy từ root `DAS-Collaboration` với Python3.12 và .NET SDK10:

```sh
python tools/export-database-schema.py --output .artifacts/qa/schema-review-01
```

Output phải mới. Tool dùng design-time factories với connection design-only, không khởi động host/seed/worker, không đọc runtime connection/JWT/SMTP từ environment. Nó cài CLI EF9.0.0/10.0.3 vào output, dùng NuGet public config và lockfile, build Debug, kiểm model/snapshot, liệt kê migration không kết nối DB và xuất SQL idempotent. Các script/hash/IDs nằm trong `sql/` và `summary.json`. Exit0 chỉ khi đủ sáu store và nguồn ứng dụng giữ nguyên. Output này không chứng minh đã thực thi SQL.

Review schema, constraints/index/FK, reference catalog seed và migration lịch sử trước áp dụng. Không sửa/xóa ID migration đã dùng. Không xuất credential, DB, backup hoặc dữ liệu khách hàng vào Git. Sinh lại script từ source được chốt thay vì giữ nhiều bản SQL copy không rõ phiên bản.

## Database SQL mới, chưa có dữ liệu

Người vận hành được cấp quyền chuẩn bị từng database và áp dụng SQL đã review vào đúng store. Chọn database đích và connection qua công cụ/quy trình quản trị của môi trường; tool xuất schema không có chế độ apply. Ghi migration history/hash source/script và kết quả đối soát vào biên bản triển khai. Không chạy script vào DB công ty chỉ vì nó có tên giống cấu hình mẫu.

Application Production phải dùng `Database:Provider=SqlServer`, connection phù hợp và `Database:Initialize=false` cho cả sáu service; Auth/Document cần `Database:SeedDemoUsers=false` và Partner cần `Database:SeedExamples=false`. `InitializeOnStartup` không phải flag các service này đọc. Provision schema bằng danh tính vận hành riêng; application không tự migration tại startup. Bật host/worker/tích hợp sau khi config/quyền/storage/audience được bàn giao và kiểm chứng.

Email Worker có baseline `20261006121013_EmailWorkerBaseline`: ba bảng trong schema `emailworker`, index ScanLogId và cascade FK scan-item→scan-log, không seed mật khẩu/hộp thư. Startup SQL kiểm snapshot/pending migration và đọc ba bảng; SQLite chỉ được Development. Notification Development SQL initialize dùng migration, SQLite dùng EnsureCreated. Các flag IMAP/SMTP/notification/reminder/scanner vẫn cần cấu hình riêng; tạo schema không bật transport.

## Database cũ hoặc đã tạo bằng EnsureCreated

Không tự coi một database có bảng là đã áp dụng migration. Không chạy baseline tạo bảng vào dữ liệu đang dùng, không tự thêm/xóa `__EFMigrationsHistory`, không tự đánh dấu “đã migrate”. Email SQL có bảng nhưng thiếu migration history sẽ bị startup từ chối khi initialization tắt.

Trước khi nâng cấp: cần export/schema snapshot/PDF, kiểm cấu trúc/kiểu dữ liệu/keys/index/FK/reference codes và mapping IDs, chạy preflight read-only, backup và diễn tập restore trên bản sao được cho phép. Sau đối soát, người phụ trách migration lập phương án nâng cấp hoặc baseline được duyệt; dữ liệu/hash/counters/history phải được kiểm lại. Chưa có export/mapping khách hàng nên dự án chưa hoàn tất bước này.

Counter, số đăng ký, audit/cancel restore, PDF claim và outbox/task/reminder ledger đã có migration/kiểm thử nội bộ. Những kiểm thử đó không tự chứng minh dữ liệu legacy phù hợp.

## Kiểm chứng SQL giả lập

```sh
python tools/qa/run-isolated-sql.py --profile core --output .artifacts/qa/sql-schema-01
python tools/qa/run-isolated-sql.py --profile core --service EmailWorkerService --output .artifacts/qa/sql-email-01
```

Core bao phủ sáu store; `--service` giới hạn một store và được ghi trong summary, không được gọi đó là đã qua cả sáu. Runner tạo SQL Server instance Docker mới, connection/mật khẩu QA sinh riêng, build trong output riêng; không nhận DB/connection có sẵn. Chỉ exit0 khi tests thật pass, source integrity và cleanup xác nhận. Worker/IMAP/SMTP/OCR/EAP không chạy.

Profile `restore` hiện chỉ bao phủ **năm store core + PDF**, chưa bao phủ EmailWorker settings/logs. Không tuyên bố sáu-store restore hoặc production RPO/RTO từ kết quả năm-store. Khi mailbox được đưa vào vận hành, bổ sung EmailWorker vào phương án backup/restore và xử lý secret theo quy trình được duyệt.

## Gate còn mở

Customer export/PDF/mapping, cấu hình/quyền deployment, authority/session/CSRF/revocation, TMS/SMTP/audience/scanner, RPO/RTO/SLA, security/dependency/license, UAT/signoff/pilot. EAP do nhóm khác phụ trách; OCR hoãn. SQLite và tài khoản demo dùng để xem local, không là database/tài khoản production.
