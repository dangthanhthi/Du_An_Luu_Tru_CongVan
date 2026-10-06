# Backend

.NET 10, 6 service và Gateway. Mỗi service sở hữu DbContext, entity, migration và lockfile riêng. Chỉ giao tiếp qua HTTP contracts; service không reference project hoặc DB của service khác. `shared/PdfProtocol` là thư viện giao thức chung, không phải schema DB chung.

| Service | Nhiệm vụ |
|---|---|
| `auth-service` | Xác thực, session/token và directory/quyền hiện có |
| `document-service` | Công văn, counter, query/resource policy, vòng đời, PDF relation, báo cáo, reminder/task/My Staff |
| `partner-service` | Đối tác/cơ quan, audit và khôi phục |
| `files-service` | Tệp/PDF, claims/quarantine/scanner/current-file protocol |
| `notification-service` | Durable inbox/outbox, dedup/receipt/history và delivery |
| `email-worker-service` | Bộ quét/xử lý email cũ; chưa chạy host trong bản kiểm chứng này |
| `gateway` | Routing và kiểm tra cấu hình/token |

```sh
dotnet restore backend/DocumentAdministration.slnx --locked-mode
dotnet build backend/DocumentAdministration.slnx --no-restore -c Release
python tools/run-checks.py --profile backend --output .artifacts/qa/backend
```

Chạy các lệnh trên từ root repository. Non-SQL tests là lựa chọn mặc định. SQL/restore/load tests cần môi trường QA được chỉ định, không dùng DB công ty.

Khi chạy host, đặt `ASPNETCORE_URLS`, provider/connection string, JWT và các inter-service settings trên máy. Gateway và Auth dùng cấu hình JWT tương thích; file claim/scanner/directory settings phải được chỉ định. Các core service có startup guards để chặn thiếu cấu hình/tự tạo schema ở Production. Email Worker cũ vẫn gọi `EnsureCreated` và đăng ký background worker trong `Program.cs`; không bật host này trên môi trường thật trước khi rà soát startup/transport. Đọc `Program.cs`, `Data/*Startup*` và config mẫu của đúng service trước khi bật host.

Migration ở `../database/migrations`; workflow ở `../workflows/business`. MSBuild props liên kết vào assembly đúng chủ sở hữu. Khi debug, IDE hiển thị các file này dưới `Migrations` và `Workflows`.

OCR service/tests đã hoãn và không nằm trong solution cộng tác này. Interface adapter cũ cần để các service compile vẫn được giữ, transport không được bật như một tích hợp đã nghiệm thu.

.NET đọc cấu hình từ JSON và biến môi trường với dấu `__` cho từng cấp, ví dụ `Jwt__Secret`, `ConnectionStrings__Default`. Secret phải được đặt riêng trên máy, tối thiểu 32 bytes cho JWT. `ASPNETCORE_URLS` chọn địa chỉ/port của host; kiểm routing ở `gateway/ocelot.json` để khớp host đã cấu hình. Dotnet không tự nạp `.env`; `.env.example` là các tên biến mẫu từ môi trường cũ, không thay thế cấu hình startup của từng service. Không dùng thông tin mẫu này để kết nối DB công ty.
