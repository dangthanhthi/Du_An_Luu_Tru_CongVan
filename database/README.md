# Database DAS — bàn giao trên nhánh `database`

Repository làm việc chính thức: **https://github.com/Seleton-VN/Intern-DocumentAdministration-BE**. Nhánh này bàn giao schema database DAS đã phát triển ở local và hướng dẫn sử dụng. Các service, cấu hình, deployment và tests có sẵn trên `main` được giữ nguyên.

## Đọc từ đâu

1. Mở **[hướng dẫn database HTML](DAS_DATABASE_HUONG_DAN_CHI_TIET.html)**: dictionary 43 bảng nghiệp vụ/360 cột, PK, FK, index, CHECK, transaction và quy tắc nghiệp vụ. Tải file về rồi mở bằng trình duyệt; không cần backend. GitHub hiển thị source HTML, không render trang.
2. Đọc **[PROVISIONING.md](PROVISIONING.md)** trước khi tạo database thử nghiệm bằng SQL.
3. **[DATABASE-HANDBOOK.md](DATABASE-HANDBOOK.md)** là bản Markdown của tài liệu chi tiết.
4. **[sql/README.md](sql/README.md)** giải thích sáu script và kiểm hash từ **[PACKAGE-MANIFEST.json](PACKAGE-MANIFEST.json)**.

## Nội dung được bàn giao

| Thư mục/file | Nội dung |
|---|---|
| `sql/*.sql` | Sáu script SQL Server idempotent, xuất từ EF migrations đã kiểm model/snapshot; dùng từng script cho đúng store |
| `migrations/<service>/` | 24 migration, các file Designer và sáu model snapshot; giữ nguyên ID/namespace |
| `DATABASE-HANDBOOK.md`, `DAS_DATABASE_HUONG_DAN_CHI_TIET.html` | Tài liệu database, ràng buộc, quy tắc, lịch sử và dictionary |
| `PROVISIONING.md` | Hướng dẫn tạo database riêng và kiểm sau chạy |
| `PACKAGE-MANIFEST.json` | SHA-256/size của mọi file trong gói, mốc source và trạng thái kiểm chứng |

Không có database thật, backup, PDF công văn, dữ liệu khách hàng, tài khoản demo, credential, frontend hoặc output build. Catalog/role tham chiếu trong SQL là seed cấu trúc của migration; không phải bản sao dữ liệu khách hàng. Không đưa Prisma template frontend vào gói này.

## Phạm vi bàn giao

Gói database này được bàn giao độc lập với backend cũ đang có trên `main`, theo yêu cầu của người dùng. Lượt push này chỉ thêm thư mục `database/`, không sửa service, ghép model hoặc thực hiện nâng cấp database.

Các đường dẫn `backend/...`, `workflows/...`, `tools/...` và lệnh QA trong handbook/HTML mô tả cây nguồn local lúc xây schema. Những thành phần runtime/công cụ đó không nằm trong gói bàn giao chỉ database này. Lệnh sử dụng trực tiếp được ở nhánh hiện tại nằm trong PROVISIONING.md và sql/README.md.

## Quy tắc đã chốt

- Ba bộ đếm công văn theo loại và năm, dùng chung giữa company/phòng. Số thứ tự mặc định bốn chữ số, chỉ thêm chữ số khi vượt 9999.
- Registration Date lấy ngày hiện tại theo Việt Nam; Issued Date có thể là ngày quá khứ. Đổi company/phòng giữ số thứ tự và ngày đăng ký.
- Khôi phục công văn Cancelled về trạng thái trước khi hủy.
- Nhắc công văn chưa phân phối khi **quá 7 ngày** theo lịch Việt Nam; kỳ nhắc thứ Hai 08:00 UTC+7.
- My Staff thuộc DAS. Có người thuộc scope không tự cấp quyền đọc công văn/PDF của họ.
- EAP thuộc nhóm khác, hoãn cho tới khi người dùng yêu cầu; OCR và các kết nối ngoài chưa có bàn giao vẫn hoãn. Schema hỗ trợ ledger/intent không chứng minh các tích hợp đã hoạt động.

## Kiểm chứng và phạm vi

Thông tin lượt xuất SQL mới được ghi trong PACKAGE-MANIFEST.json: cả sáu model/snapshot được kiểm, migration ID được liệt kê không kết nối DB và SQL được xuất lại. Đây là kiểm chứng tạo schema offline; **không phải** triển khai SQL lên database khách hàng, UAT hoặc xác nhận backend chính thức đã chạy với schema này.

Customer mapping/export, backup/restore môi trường thật và quyền vận hành vẫn cần bàn giao.
