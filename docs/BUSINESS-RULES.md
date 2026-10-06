# Quy tắc nghiệp vụ đã chốt

- Dự kiến sử dụng từ năm 2027. Registration Date do server lấy ngày hiện tại lúc đăng ký, không hồi tố. Issued Date có thể trong quá khứ.
- Có 3 counter theo Incoming/Outgoing/Internal và năm, dùng chung giữa company/phòng. Sequence 1..9999 hiển thị 4 chữ số; từ 10000 mới 5 chữ số. Không tăng độ dài trước giới hạn.
- Cho sửa company/phòng sau đăng ký, giữ nguyên sequence/năm/registration date. Người chuyển phòng không làm thay đổi công văn cũ.
- Phòng của công văn xác định bởi originator/người thực sự gửi; khi tạo mặc định phòng chính, có thể chọn phòng khác trong các membership được cấp.
- Cancelled khôi phục về trạng thái trước hủy, không cấp số mới và không thay ownership lịch sử.
- Reminder chỉ khi quá 7 ngày; chạy thứ Hai 08:00 giờ Việt Nam. To inputter, CC LM/DLM theo projection tin cậy; ngày 7/Cancelled/Incoming bị loại theo scope đã chốt. Durable Queued/Accepted chưa là SMTP Sent.
- My Staff hiển thị trong DAS; manager xem nhân sự/task thuộc phạm vi descendants được backend cấp. Task assignment không tự cấp quyền đọc công văn.
- Admin quản trị master data, không tự có quyền đọc hoặc sửa công văn. Quyền công văn vẫn theo các vai trò và phạm vi nghiệp vụ được cấp, kể cả API legacy.
- MGT có parent null. ADM/DRI/HSE là department; IT/HR là group dưới ADM. EAP ID/mapping và directory chính thức đang chờ, không tự bịa dữ liệu hoặc role.
- EAP thuộc người phụ trách khác; chỉ triển khai khi người dùng yêu cầu lại. OCR tiếp tục hoãn. SMTP/TMS adapters prepared nhưng chưa có contract/sandbox/config thật. Fax để cuối. Không gọi các phần này là đã tích hợp thật.

Quyền, counter, PDF claim và audit do server kiểm tra. UI menu/capability chỉ là hiển thị, không là nguồn quyền.
