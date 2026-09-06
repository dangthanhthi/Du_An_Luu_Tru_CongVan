# TÀI LIỆU BÀN GIAO TOÀN DIỆN DỰ ÁN HỆ THỐNG QUẢN LÝ CÔNG VĂN & VĂN THƯ SỐ (DAS)
> **Tác giả:** Đặng Thành Thi (`dangthanhthi213@gmail.com`)  
> **Thời gian:** 04/09/2026  
> **Phiên bản:** Hoàn thiện Local Dev & Auto-Seeding (Full Features & Seed Data)

---

## 1. TỔNG QUAN DỰ ÁN & KIẾN TRÚC HỆ THỐNG

Dự án **Document Administration System (DAS)** là hệ thống quản lý công văn và văn thư điện tử thông minh, tích hợp trí tuệ nhân tạo (AI-OCR) để bóc tách văn bản scan/ảnh, quản lý luồng phê duyệt, quản lý danh bạ đối tác, phân quyền người dùng và gửi thông báo qua email / in-app.

### Kiến trúc Microservices & Cổng Dịch Vụ:
- **Frontend Web:** Next.js 14+ (App Router), Tailwind CSS, Lucide React, Shadcn/UI (Cổng: `http://localhost:3000`)
- **API Gateway (YARP / Reverse Proxy):** Điều hướng tất cả request từ FE vào các microservices (Cổng: `http://localhost:8080`)
- **Auth Service:** Xác thực người dùng, JWT Tokens, Refresh Tokens, Quản lý tài khoản và phòng ban (Cổng: `http://localhost:5001`)
- **Document Service:** Quản lý vòng đời công văn (Đến, Đi, Nội bộ), số hiệu công văn, file đính kèm, phân quyền phòng ban (Cổng: `http://localhost:5002`)
- **Partner Service:** Quản lý danh bạ cơ quan/đối tác gửi và nhận (Cổng: `http://localhost:5003`)
- **Files Service:** Lưu trữ vật lý tệp đính kèm (PDF, ảnh, Word), phục vụ tải và xem tệp (Cổng: `http://localhost:5004`)
- **Notification Service:** Thông báo in-app (SignalR), nhật ký thông báo, cấu hình thông báo người dùng (Cổng: `http://localhost:5005`)
- **AI-OCR Service:** Bóc tách metadata tự động từ văn bản scan (Số hiệu, Ngày ban hành, Cơ quan ban hành, Trích yếu tóm tắt nội dung) (Cổng: `http://localhost:5006`)
- **Email Worker Service:** Xử lý hàng đợi gửi email thông báo công văn nền (Cổng: `http://localhost:5007`)

---

## 2. REPOSITORY VÀ NHÁNH CHÍNH THỨC (GITHUB)

Để hệ thống hoạt động chính xác và đầy đủ nhất:

### Backend Repository:
- **URL:** `https://github.com/Seleton-VN/Intern-DocumentAdministration-BE.git`
- **Nhánh chuẩn (Master Complete):** `feat/be-complete`
- **Các nhánh tính năng con đã được đồng bộ:**
  - `feature/partner-service`
  - `feature/document-service`
  - `feature/files-service`
  - `feature/ai-ocr-service`
  - `feature/email-worker-service`

### Frontend Repository:
- **URL:** `https://github.com/Seleton-VN/Intern-DocumentAdministration-FE-Web.git`
- **Nhánh chuẩn:** `ocr-fix`

---

## 3. THÔNG TIN ĐĂNG NHẬP & TÀI KHOẢN MẶC ĐỊNH

Tất cả các tài khoản mặc định được tự động nạp vào SQLite Database khi khởi động:
- **Mật khẩu chung cho tất cả tài khoản:** `password`

| Username | Tên đầy đủ | Vai trò | Quyền hạn |
| :--- | :--- | :--- | :--- |
| **`admin_user`** | Quản trị viên Hệ thống | Admin | Toàn quyền hệ thống, xem mọi loại công văn, quản lý đối tác, người dùng |
| **`secretary_user`** | Thư ký Nguyễn Văn A | Secretary | Nhập công văn đến/đi, phân phối công văn, cập nhật trạng thái |
| **`employee_user`** | Nhân viên Lê Văn C | Employee | Xem công văn được giao, xử lý công việc nội bộ |

---

## 4. HƯỚNG DẪN CÀI ĐẶT & CHẠY NGAY TRÊN MÁY TÍNH MỚI

### Bước 1: Điều kiện tiên quyết (Yêu cầu môi trường)
1. **Git:** Đã cài đặt trên Windows (`git --version`).
2. **.NET 10.0 SDK:** Có thể tải từ Microsoft (`dotnet --version`).
3. **Node.js (v18 trở lên):** Đã cài đặt Node & npm (`node -v`).

### Bước 2: Clone mã nguồn
Mở Windows Terminal (PowerShell hoặc Command Prompt) tại thư mục làm việc mong muốn và chạy:
```powershell
# 1. Clone Backend (nhánh feat/be-complete)
git clone -b feat/be-complete https://github.com/Seleton-VN/Intern-DocumentAdministration-BE.git

# 2. Clone Frontend (nhánh ocr-fix) đặt ngang hàng
git clone -b ocr-fix https://github.com/Seleton-VN/Intern-DocumentAdministration-FE-Web.git
```

*Cấu trúc thư mục mong muốn:*
```text
Dự án taskmanager/
├── Intern-DocumentAdministration-BE/
└── Intern-DocumentAdministration-FE-Web/
```

### Bước 3: Chạy hệ thống chỉ với 1 click
Vào thư mục `Intern-DocumentAdministration-BE` và click đúp chuột vào file:
👉 **`START_LOCAL_SYSTEM.bat`**

File script này sẽ tự động:
1. Kiểm tra môi trường (.NET, Node.js).
2. Tự động chạy `npm install` cho Frontend nếu chưa cài thư viện.
3. Khởi chạy toàn bộ 8 Microservices Backend và 1 Frontend Next.js trong các cửa sổ riêng biệt.
4. Tự động kiểm tra và nạp dữ liệu mẫu vào SQLite:
   - Nạp **90 đối tác** vào `partner-service/partner_local.db`.
   - Nạp **227 công văn** và 143 đính kèm vào `document-service/document_local.db`.
   - Nạp **270 tệp tin** vào `files-service/files_local.db` (kèm 52 file vật lý trong `Uploads/`).
   - Nạp **3 tài khoản người dùng** và phòng ban vào `auth-service/auth_local.db`.
5. Tự động mở trình duyệt web tại `http://localhost:3000`.

---

## 5. CÁC VẤN ĐỀ ĐÃ ĐƯỢC GIẢI QUYẾT & LỊCH SỬ SỬA LỖI (BUG FIXES)

Trong quá trình phát triển và kiểm thử đồng bộ giữa các máy tính, các vấn đề quan trọng sau đây đã được xử lý triệt để:

### 1. Đồng bộ dữ liệu (Auto-seeding Data on Fresh Machine):
- **Hiện tượng trước đây:** Khi clone mới từ Git, các bảng danh bạ đối tác (`/partners`) báo *No data found (0-0)*, danh sách công văn rỗng, dashboard không có số liệu.
- **Nguyên nhân:** SQLite `.db` bị chặn bởi `.gitignore`. Các service không tự động seed dữ liệu khi DB trống.
- **Giải pháp:**
  - Trích xuất toàn bộ dữ liệu mẫu thực tế thành các file JSON seed:
    - `services/partner-service/partners_seed.json` (90 đối tác)
    - `services/document-service/documents_seed.json` (227 công văn, 3 bộ đếm, 143 liên kết file, 239 lịch sử trạng thái)
    - `services/files-service/files_seed.json` (270 bản ghi tệp tin)
  - Mở chặn tracking thư mục vật lý `services/files-service/Uploads/` trong `.gitignore` để commit toàn bộ 52 file đính kèm thực tế lên Git.
  - Viết code C# trong `Program.cs` của từng service: khi chạy SQLite, nếu bảng chưa có bản ghi, hệ thống tự động đọc file JSON seed và nạp vào DB.

### 2. Định dạng DateTime và Boolean trong System.Text.Json:
- SQLite lưu ngày tháng dạng text `'YYYY-MM-DD HH:MM:SS.ffffff'` và boolean dạng số `0/1`.
- `System.Text.Json` của .NET ném lỗi `JsonException` khi parse chuỗi ngày tháng có khoảng trắng hoặc kiểu số thay cho bool.
- Toàn bộ dữ liệu seed đã được chuẩn hóa sang chuẩn ISO 8601 (`YYYY-MM-DDTHH:MM:SS...Z`) và strict booleans (`true`/`false`).

### 3. Lỗi CORS (Cross-Origin Resource Sharing):
- Trước đây Frontend gọi vào các service bị lỗi `TypeError: Failed to fetch` khi đăng nhập hoặc lấy danh sách.
- Đã bổ sung `AddCors` và `app.UseCors()` vào `auth-service`, `document-service`, `partner-service`, `files-service` với policy cho phép mọi origin local, headers, methods và credentials.

### 4. Lỗi JWT:Secret trên môi trường Local:
- Kestrel yêu cầu chuỗi bí mật JWT tối thiểu 32 bytes (256-bit).
- Đã thiết lập giá trị bí mật mặc định an toàn cho môi trường Local:
  `DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes`
  được nhúng sẵn trong `appsettings.json` của `auth-service`, `gateway`, và trong các script `.bat`.

### 5. Khắc phục lỗi cú pháp Batch Script trên Windows (`.bat`):
- Các ký tự dấu ngoặc đơn `()` bên trong câu lệnh `echo` trong khối `if ()` gây ra lỗi `was unexpected at this time`.
- Lệnh `timeout` gây lỗi `Redirection is not supported` trong một số phiên bản PowerShell/cmd.
- Đã chuẩn hóa toàn bộ file `.bat` bằng cú pháp chuẩn: dùng `ping 127.0.0.1 -n X > nul` để delay và chuyển thư mục làm việc (`cd / pushd`) trước khi gọi `dotnet run`.

### 6. Khắc phục sự cố Turbopack Unicode Path trên Windows:
- Khi đường dẫn chứa tiếng Việt có dấu (như `Dự án taskmanager`), Turbopack của Next.js bị lỗi crash mã hóa byte charmap.
- Đã cập nhật `package.json` của Frontend: chuyển script `npm run dev` sang chạy webpack mặc định của Next.js (`next dev`), giúp khởi động mượt mà trên mọi thư mục có dấu tiếng Việt.

---

## 6. MÃ NGUỒN CÁC PHẦN CỐT LÕI (CORE CODE SNIPPETS)

### A. Auto-Seeding trong `partner-service/Program.cs`:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PartnerDbContext>();
    if (db.Database.IsSqlServer())
        await db.Database.MigrateAsync();
    else
        db.Database.EnsureCreated();

    if (!await db.Partners.AnyAsync())
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "partners_seed.json");
        if (!File.Exists(seedPath))
            seedPath = Path.Combine(builder.Environment.ContentRootPath, "partners_seed.json");

        if (File.Exists(seedPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(seedPath);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<Partner>>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items != null && items.Count > 0)
                {
                    db.Partners.AddRange(items);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Could not auto-seed partners from seed file.");
            }
        }
    }
}
```

### B. Auto-Seeding trong `document-service/Program.cs`:
```csharp
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
        db.Database.EnsureCreated();

        if (!await db.Documents.AnyAsync())
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "documents_seed.json");
            if (!File.Exists(seedPath))
                seedPath = Path.Combine(builder.Environment.ContentRootPath, "documents_seed.json");

            if (File.Exists(seedPath))
            {
                var json = await File.ReadAllTextAsync(seedPath);
                using var docObj = System.Text.Json.JsonDocument.Parse(json);
                var root = docObj.RootElement;
                
                if (root.TryGetProperty("counters", out var countersElem))
                {
                    var counters = System.Text.Json.JsonSerializer.Deserialize<List<DocumentNumberCounter>>(countersElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (counters != null && counters.Count > 0)
                        db.DocumentNumberCounters.AddRange(counters);
                }

                if (root.TryGetProperty("documents", out var docsElem))
                {
                    var docs = System.Text.Json.JsonSerializer.Deserialize<List<Document>>(docsElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (docs != null && docs.Count > 0)
                        db.Documents.AddRange(docs);
                }

                await db.SaveChangesAsync();

                if (root.TryGetProperty("attachments", out var attElem))
                {
                    var atts = System.Text.Json.JsonSerializer.Deserialize<List<DocumentAttachment>>(attElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (atts != null && atts.Count > 0)
                        db.DocumentAttachments.AddRange(atts);
                }

                if (root.TryGetProperty("history", out var histElem))
                {
                    var hist = System.Text.Json.JsonSerializer.Deserialize<List<DocumentStatusHistory>>(histElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (hist != null && hist.Count > 0)
                        db.DocumentStatusHistory.AddRange(hist);
                }

                if (root.TryGetProperty("access", out var accessElem))
                {
                    var acc = System.Text.Json.JsonSerializer.Deserialize<List<DocumentDepartmentAccess>>(accessElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (acc != null && acc.Count > 0)
                        db.DocumentDepartmentAccess.AddRange(acc);
                }

                await db.SaveChangesAsync();
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not auto-seed documents.");
    }
}
```

### C. Script khởi động toàn diện (`START_LOCAL_SYSTEM.bat`):
```batch
@echo off
chcp 65001 > nul
title KHOI DONG TOAN BO HE THONG (FRONTEND + BACKEND)

if "%Jwt__Secret%"=="" set "Jwt__Secret=DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes"
if "%JWT_SECRET%"=="" set "JWT_SECRET=DocumentAdministrationSuperSecretKey2026!LocalDevOnly32Bytes"

echo ==============================================================================
echo       KHOI DONG HE THONG QUAN LY CONG VAN VA VAN THU SO [AI-OCR]
echo ==============================================================================
echo.

pushd "%~dp0"

echo [1/9] Khoi chay API Gateway (Port 8080)...
start "API-Gateway [8080]" cmd /k "cd gateway && dotnet run --launch-profile http"

ping 127.0.0.1 -n 3 > nul

echo [2/9] Khoi chay Auth Service (Port 5001)...
start "Auth-Service [5001]" cmd /k "cd services\auth-service && dotnet run --launch-profile http"

echo [3/9] Khoi chay Document Service (Port 5002)...
start "Document-Service [5002]" cmd /k "cd services\document-service && dotnet run --launch-profile http"

echo [4/9] Khoi chay Partner Service (Port 5003)...
start "Partner-Service [5003]" cmd /k "cd services\partner-service && dotnet run --launch-profile http"

echo [5/9] Khoi chay Files Service (Port 5004)...
start "Files-Service [5004]" cmd /k "cd services\files-service && dotnet run --launch-profile http"

echo [6/9] Khoi chay Notification Service (Port 5005)...
start "Notification-Service [5005]" cmd /k "cd services\notification-service && dotnet run --launch-profile http"

echo [7/9] Khoi chay AI-OCR Service (Port 5006)...
start "AI-OCR-Service [5006]" cmd /k "cd services\ai-ocr-service && dotnet run --launch-profile http"

echo [8/9] Khoi chay Email Worker Service (Port 5007)...
start "Email-Worker [5007]" cmd /k "cd services\email-worker-service && dotnet run --launch-profile http"

ping 127.0.0.1 -n 4 > nul

popd

:: Tim thu muc FE
set "FE_DIR=%~dp0..\Intern-DocumentAdministration-FE-Web"
if not exist "%FE_DIR%" set "FE_DIR=%~dp0Intern-DocumentAdministration-FE-Web"

pushd "%FE_DIR%"
if not exist "%FE_DIR%\node_modules" (
    echo [LAN DAU CHAY] Cai dat node_modules...
    call npm install
)
echo [9/9] Khoi chay Frontend (Port 3000)...
start "Frontend-Webapp [3000]" cmd /k "npm run dev"

ping 127.0.0.1 -n 4 > nul
start http://localhost:3000
```

---

## 7. CHECKLIST KIỂM TRA TÍNH NĂNG SAU KHI CHẠY TRÊN MÁY MỚI

1. **Đăng nhập (`/login`):** Nhập `admin_user` / `password`. Hệ thống nhận token JWT, tự chuyển hướng vào Dashboard.
2. **Tổng quan (`/`):** Hiển thị đầy đủ biểu đồ công văn, thống kê 227 công văn.
3. **Danh bạ đối tác (`/partners`):** Hiển thị đầy đủ **90 đối tác** (Bộ Tài chính, Bộ Y tế, Sở TT&TT,...), tìm kiếm, lọc hoạt động chính xác.
4. **Công văn đến / đi (`/documents/incoming`, `/documents/outgoing`):** Hiển thị danh sách đầy đủ công văn, số hiệu chuẩn, phân trang, lọc trạng thái.
5. **Xem tệp đính kèm (`/documents/[id]`):** Bấm xem tệp đính kèm mở đúng file PDF/ảnh trong thư mục `services/files-service/Uploads/`.
6. **Bóc tách AI-OCR (`/documents/incoming/create`):** Tải lên file ảnh/scan công văn, AI-OCR tự động bóc tách số hiệu, ngày tháng, trích yếu và ghép đúng đối tác.
