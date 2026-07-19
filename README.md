# CV - Hệ thống Quản lý & Lưu trữ Công văn Doanh nghiệp

**CV (Document Archive System)** là một giải pháp quản lý, điều hành và lưu trữ công văn số hóa toàn diện dành cho doanh nghiệp. Hệ thống được thiết kế với ngôn ngữ tối giản (minimalism), tối ưu hóa trải nghiệm người dùng theo phong cách thiết kế cao cấp của Vercel (Dark Mode mặc định, đường nét sắc sảo, phản hồi nhanh).

---

## ✨ Tính năng nổi bật

### 📊 1. Bảng điều khiển thông minh (Interactive Dashboard)
Thống kê lưu lượng công văn đến, đi và nội bộ theo thời gian thực dưới dạng biểu đồ cột trực quan.

Giám sát tài nguyên hệ thống trực tiếp (Dung lượng cơ sở dữ liệu SQLite, Tốc độ CPU, bộ nhớ RAM, tình trạng Email Watcher).

Liệt kê công văn mới cập nhật cùng trạng thái phân phối.

### 📁 2. Quản lý luồng công văn đa dạng
**Công văn đến (Incoming):** Đăng ký thông tin công văn nhận được từ đối tác, phân phối đến các phòng ban chịu trách nhiệm.

**Công văn đi (Outgoing):** Soạn thảo, trình ký duyệt nội bộ và phát hành ra bên ngoài.

**Văn bản nội bộ (Internal):** Lưu trữ và truyền tải thông tin chỉ thị, thông báo giữa các phòng ban.

**Trình xem PDF trực quan:** Nhúng trực tiếp bản xem trước PDF (Iframe Preview) của tài liệu vật lý giúp người dùng đọc nội dung ngay trên trình duyệt mà không cần tải về.

### 🔑 3. Xác thực bảo mật Vercel-style
Hỗ trợ màn hình đăng nhập và đăng ký Vercel tối giản trên nền đen sâu thẳm.

Hỗ trợ xác thực truyền thống (Tài khoản/Mật khẩu) và liên kết tài khoản SSO nhanh qua các cổng dịch vụ **Google**, **GitHub**, **Apple**.

Cơ chế liên kết tài khoản động: Nếu đăng nhập SSO lần đầu chưa có tài khoản, hệ thống sẽ mở form đăng ký và liên kết thông tin phòng ban, chức vụ tương ứng ngay lập tức.

**Tài khoản dùng thử mặc định:**
Tài khoản: `admin` | Mật khẩu: `123456`

### ✍️ 4. Ký số điện tử bảo mật (Digital Signing)
Cho phép Giám đốc hoặc lãnh đạo phòng ban thực hiện phê duyệt và ký số điện tử bảo mật bằng mã PIN xác thực (`1234`).

Tự động ghi lại nhật ký phê duyệt (Timeline Audit Log) bao gồm thời gian, người ký và trạng thái tài liệu.

### 🤝 5. Danh bạ đối tác gửi/nhận nâng cao
Trình quản lý đối tác tương tác cao với đầy đủ tính năng Thêm, Sửa, bộ lọc Tìm kiếm nhanh kiểu Unsplash (thanh tìm kiếm hình viên thuốc tích hợp icon quét OCR).

**Tính năng Soft Delete (Xóa mềm):** Cho phép ẩn tạm thời đối tác (giảm độ hiển thị xuống 50%) và khôi phục nhanh chóng bằng 1 click để tránh mất mát dữ liệu.

### 🏢 6. Tổ chức phòng ban & Người dùng
Phân cấp quản lý tài liệu theo phòng ban (Ban Giám đốc, Phòng Kế hoạch, Phòng Tài chính, Phòng Hành chính).

Kiểm soát quyền truy cập công văn chặt chẽ dựa trên chức vụ và bộ phận làm việc.

---

## 🛠️ Công nghệ sử dụng (Technology Stack)

### 💻 Frontend (Ứng dụng Web)
**Framework:** Next.js (App Router) v16.2

**Ngôn ngữ:** TypeScript

**Styling:** Tailwind CSS v4 (Sử dụng hệ màu kẽm trung tính `zinc` thuần xám loại bỏ sắc xanh lam)

**Icons:** Custom SVG inline

**State Management:** React Hooks & LocalStorage lưu trữ giả lập cơ sở dữ liệu phía máy khách

### 🖥️ Backend (Dịch vụ API)
**Framework:** .NET 10.0 (ASP.NET Core Web API)

**Ngôn ngữ:** C# 14

**API Style:** GraphQL (Thông qua thư viện `HotChocolate.AspNetCore`)

**Database:** SQLite (`taskmanager.db` gọn nhẹ, tốc độ cao)

**ORM:** Entity Framework Core

**Xác thực mở rộng:** LDAP (`Novell.Directory.Ldap`) kết nối Active Directory doanh nghiệp

---

## 🚀 Hướng dẫn cài đặt và khởi chạy cục bộ

### Yêu cầu hệ thống
Đã cài đặt **Node.js** (v18 trở lên)

Đã cài đặt **.NET SDK 10.0**

### 1. Khởi động Backend API
Di chuyển vào thư mục backend và chạy lệnh:
```bash
cd TaskManager.Backend
dotnet run --urls=http://127.0.0.1:5000
```

### 2. Khởi động Frontend Webapp
Di chuyển vào thư mục frontend, cài đặt thư viện và chạy máy chủ phát triển:
```bash
cd DAS/src/webapp
npm install
npm run dev
```
Truy cập ứng dụng tại địa chỉ: **[http://localhost:3000/](http://localhost:3000/)**

### 📦 3. Đóng gói cho Production
**Backend:**
```bash
dotnet publish -c Release -o ./publish
```

**Frontend:**
```bash
npm run build
npm run start
```

---

## 🔒 Cài đặt tài khoản kiểm thử mặc định
**Tài khoản quản trị:** `admin` | Mật khẩu: `123456`

**Mã PIN ký số:** `1234`

**Địa chỉ API cục bộ:** `http://127.0.0.1:5000`
