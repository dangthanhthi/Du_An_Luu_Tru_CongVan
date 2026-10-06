# Email intake — cấu hình và giới hạn vận hành

Email intake là dịch vụ đọc hộp thư/đưa công văn đến vào DAS. Đây là luồng riêng với email nhắc hạn gửi từ Notification Service.

## Mặc định

- `EmailIntake:WorkerEnabled=false`: không đăng ký worker quét định kỳ.
- `EmailIntake:ManualScanEnabled=false`: test IMAP, quét thủ công và confirm-intake trả 503 trước khi gọi transport/service ngoài. Cờ này không tự bật worker định kỳ.
- `Database:Initialize=false`: không tạo schema khi khởi động. Dịch vụ đọc kiểm tra ba bảng hiện có và dừng nếu schema thiếu.
- `Database:Provider` phải là `SqlServer` hoặc `Sqlite`, cùng `ConnectionStrings:Default` hợp lệ. Production chỉ chấp nhận SQL Server, database được provision riêng và `Initialize=false`.
- Development có thể tạo SQLite QA bằng `Database:Provider=Sqlite`, connection tới file riêng và `Database:Initialize=true`. Không áp dụng cách này với dữ liệu khách hàng.

Trong cấu hình environment, thay dấu `:` bằng `__`, ví dụ `EmailIntake__WorkerEnabled=false`. Không đặt mật khẩu vào Git hoặc frontend.

## Trước khi bật intake thật

Phải có schema SQL được bàn giao và kiểm tra, cấu hình hộp thư, whitelist được duyệt, quyền người vận hành và các service tạo công văn/lưu file hoạt động. Bộ này chưa nghiệm thu email intake Production; không coi phép bật flag là đã nghiệm thu. OCR vẫn hoãn: không bật intake thật khi luồng xử lý hiện hành còn phụ thuộc OCR chưa được bàn giao. Codex không triển khai EAP.

Các kiểm thử startup dùng SQLite tạm và processor fixture, không gọi IMAP/SMTP/OCR/EAP hoặc gửi thư thật. Startup đọc schema không chứng minh khả năng backup/restore hay tính tương thích của dữ liệu khách hàng.
