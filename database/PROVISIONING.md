# Tạo database thử nghiệm từ bộ SQL bàn giao

Áp dụng cho bộ schema trong nhánh `database` của repo chính thức. Bộ database được bàn giao độc lập; lượt bàn giao này không ghép hoặc sửa backend cũ.

## 1. Chuẩn bị

- SQL Server và danh tính được phép tạo schema/database thử nghiệm. `sqlcmd` hỗ trợ batch `GO` nếu dùng CLI; SSMS cũng có thể mở script.
- Sáu database mới, mỗi store một database. Tên ví dụ dưới đây chỉ minh họa: `DAS_Auth_Review`, `DAS_Document_Review`, `DAS_Files_Review`, `DAS_Notification_Review`, `DAS_Partner_Review`, `DAS_Email_Review`.
- Kiểm SHA-256 của gói theo [sql/README.md](sql/README.md). Đọc DDL, seed tham chiếu và migration guard trước khi chạy.
- Không cần Node, frontend, EAP, Docker hay build backend để đọc tài liệu/chạy sáu SQL script đã xuất. Khi tái sinh SQL hoặc dùng EF migration bằng code cần đúng model, project và tool của nguồn đã đồng bộ.

## 2. Gán đúng store

| Script | Store/schema | Bảng nghiệp vụ | Context nguồn | Tool EF khi xuất |
|---|---|---:|---|---|
| `database/sql/auth.sql` | Auth / `auth` | 8 | AuthDbContext | 10.0.3 |
| `database/sql/document.sql` | Document / `document` | 23 | DocumentDbContext | 10.0.3 |
| `database/sql/files.sql` | Files / `files` | 3 | FileDbContext | 10.0.3 |
| `database/sql/notification.sql` | Notification / `notification` | 4 | NotificationDbContext | 10.0.3 |
| `database/sql/partner.sql` | Partner / `partner` | 2 | PartnerDbContext | 10.0.3 |
| `database/sql/email.sql` | Email Worker / `emailworker` | 3 | EmailWorkerDbContext | 9.0.0 |

Mỗi database có `dbo.__EFMigrationsHistory` riêng, ngoài 43 bảng nghiệp vụ. Không chạy tất cả script vào một database: các lịch sử có thể trùng migration ID giữa service, ví dụ `InitialCreate`.

## 3. Chạy script vào database mới

Trong SSMS, mở script, chọn đúng server/database trong dropdown, kiểm lại `SELECT DB_NAME()` và identity trước khi Execute. Scripts không chứa `CREATE DATABASE`, không tự chọn tên database và không tự bật worker.

Ví dụ PowerShell từ root repository, dùng Windows Integrated Authentication của danh tính đã được cấp quyền:

```powershell
$taskSqlServer = '<server-thu-nghiem-da-duoc-chi-dinh>'
sqlcmd -S $taskSqlServer -d DAS_Auth_Review         -E -b -i database/sql/auth.sql
sqlcmd -S $taskSqlServer -d DAS_Document_Review     -E -b -i database/sql/document.sql
sqlcmd -S $taskSqlServer -d DAS_Files_Review        -E -b -i database/sql/files.sql
sqlcmd -S $taskSqlServer -d DAS_Notification_Review -E -b -i database/sql/notification.sql
sqlcmd -S $taskSqlServer -d DAS_Partner_Review      -E -b -i database/sql/partner.sql
sqlcmd -S $taskSqlServer -d DAS_Email_Review        -E -b -i database/sql/email.sql
```

Đây là sáu thao tác độc lập. Kiểm exit code sau từng lệnh, dừng khi lỗi; không xem lượt cuối thành công là cả sáu thành công. Không thêm `-P` với mật khẩu vào command history/Git; môi trường dùng kiểu đăng nhập khác phải dùng cơ chế cấp secret của người vận hành.

## 4. Kiểm sau chạy

Trên từng database, đối chiếu các migration ID trong manifest với truy vấn:

```sql
SELECT DB_NAME() AS DatabaseName;
SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name;
SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints;
SELECT name, is_disabled, is_not_trusted FROM sys.foreign_keys;
```

Đối chiếu dictionary trong handbook về kiểu cột/NULL, PK, unique index, FK/CHECK, catalog và cấu hình singleton Email `Id=1`. Ghi hash scripts, migration history và kết quả kiểm vào biên bản môi trường đó. Idempotent dựa trên migration history; không phát hiện đầy đủ schema bị sửa thủ công hoặc dữ liệu legacy không khớp.

Không có bước seed tài khoản test hoặc công văn mẫu trong bàn giao này. Không tự tạo user/department bằng GUID đoán trước rồi coi là mapping EAP.

## 5. Database đang có dữ liệu

Không dùng quy trình database mới để nâng một database có sẵn. Trước hết cần backup, restore thử trên bản sao, snapshot schema, history, reference codes và mapping dữ liệu/PDF. Không tự xóa/chèn history để bỏ qua migration; không ghép hai lịch sử chỉ vì ID đầu tiên giống nhau.

Migration Email `20261006154722_FixedEmailSettingsKey` dựng lại bảng cấu hình trong transaction để bỏ IDENTITY, bảo toàn cấu hình hiện có. Guard từ chối khóa khác 1 hoặc FK/index/trigger/quyền/default/CHECK tùy chỉnh chưa được nhận diện. Khi bị từ chối cần phương án riêng được review; không bỏ guard để ép chạy.

## 6. Trước khi đưa runtime vào sử dụng

Phải đồng bộ entity/DbContext/workflow với schema và chạy regression/SQL tests của bản tích hợp. Cấu hình provider/connection theo từng host; không suy ra sáu store dùng chung vì đều có connection key `Default`.

Ở nguồn local xây gói này, Auth/Document/Files/Notification/Email đọc `Database:Initialize=false`; Partner đọc `Database:InitializeOnStartup=false`; các flag seed demo cũng tắt. Các quy định runtime trong handbook mô tả nguồn local hiện hành.

Tạo schema không bật SMTP/IMAP/reminder/task/scanner. Byte PDF nằm ở storage riêng; SQL backup không chứa byte PDF. Không đưa dữ liệu, backup, secret, database test hoặc PDF lên nhánh này.
