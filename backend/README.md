# Backend

.NET 10, 6 service và Gateway. Mỗi service sở hữu DbContext, entity, migration và lockfile riêng. Chỉ giao tiếp qua HTTP contracts; service không reference project hoặc DB của service khác. `shared/PdfProtocol` là thư viện giao thức chung, không phải schema DB chung.

Trước khi ghép các module thành viên 1, xem [dependency và mapping nguồn](../docs/contracts/MEMBER1-DEPENDENCY-MAP.md) và [quy ước namespace/DTO hiện tại](../docs/contracts/NAMESPACE-DTO-CONVENTIONS.md). Numbering cùng Relations compile trong assembly DocumentService; không tạo bản sao model/DTO theo tên nhánh Git.

| Service | Nhiệm vụ |
|---|---|
| `auth-service` | Xác thực, session/token và directory/quyền hiện có |
| `document-service` | Công văn, counter, query/resource policy, vòng đời, PDF relation, báo cáo, reminder/task/My Staff |
| `partner-service` | Đối tác/cơ quan, audit và khôi phục |
| `files-service` | Tệp/PDF, claims/quarantine/scanner/current-file protocol |
| `notification-service` | Durable inbox/outbox, dedup/receipt/history và delivery |
| `email-worker-service` | Bộ quét/xử lý email; schema/startup có guard, worker/manual transport mặc định tắt |
| `gateway` | Routing và kiểm tra cấu hình/token |

```sh
dotnet restore backend/DocumentAdministration.slnx --locked-mode
dotnet build backend/DocumentAdministration.slnx --no-restore -c Release
python tools/run-checks.py --profile backend --output .artifacts/qa/backend
```

Chạy các lệnh trên từ root repository. Non-SQL tests là lựa chọn mặc định. SQL/restore/load tests cần môi trường QA được chỉ định, không dùng DB công ty.

Khi chạy host, đặt `ASPNETCORE_URLS`, provider/connection string, JWT và các inter-service settings trên máy. Gateway và Auth dùng cấu hình JWT tương thích; file claim/scanner/directory settings phải được chỉ định. Các core service có startup guards để chặn thiếu cấu hình/tự tạo schema ở Production. Email Worker chỉ dùng EnsureCreated cho SQLite Development; SQL dùng migration và Production không tự thay schema. Worker/manual transport mặc định tắt; schema có sẵn không đồng nghĩa intake thật đã nghiệm thu. Đọc `Program.cs`, `Data/*Startup*`, [provision database](../database/PROVISIONING.md) và config mẫu của đúng service trước khi bật host.

Migration ở `../database/migrations`; workflow ở `../workflows/business`. MSBuild props liên kết vào assembly đúng chủ sở hữu. Khi debug, IDE hiển thị các file này dưới `Migrations` và `Workflows`.

Ghép backend chung của thành viên 1 và 3 theo [phương án ghép](../docs/contracts/MEMBER1-MEMBER3-INTEGRATION-PLAN.md) và [bảng file bổ sung/thay thế](../docs/contracts/MEMBER1-MEMBER3-FILE-MAP.md). Nguồn local là bản tích hợp để đối chiếu; các folder package trên nhánh Git không tự được compile vào service.

OCR service/tests đã hoãn và không nằm trong solution cộng tác này. Interface adapter cũ cần để các service compile vẫn được giữ, transport không được bật như một tích hợp đã nghiệm thu.

.NET đọc cấu hình từ JSON và biến môi trường với dấu `__` cho từng cấp, ví dụ `Jwt__Secret`, `ConnectionStrings__Default`. Secret phải được đặt riêng trên máy, tối thiểu 32 bytes cho JWT. `ASPNETCORE_URLS` chọn địa chỉ/port của host; kiểm routing ở `gateway/ocelot.json` để khớp host đã cấu hình. Dotnet không tự nạp `.env`; `.env.example` là các tên biến mẫu từ môi trường cũ, không thay thế cấu hình startup của từng service. Không dùng thông tin mẫu này để kết nối DB công ty.
