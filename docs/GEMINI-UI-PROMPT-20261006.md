# Prompt bàn giao UI DAS

Copy nội dung bên dưới và kèm báo cáo `UI-GEMINI-HANDOFF-20261006.md`. Với MCP, cung cấp source excerpts; MCP không đọc được file chỉ bằng path.

---

Bạn hỗ trợ hoàn thiện frontend DAS hiện có. Đọc báo cáo dự án/backlog được đính kèm trước khi kết luận. Phạm vi chính là frontend Next.js/React/TypeScript/MUI trong `DAS-Collaboration/frontend`; backend/DTO là hợp đồng phải giữ. EAP do nhóm khác phụ trách, OCR đang hoãn; không triển khai hai phần này hoặc bật SMTP/TMS/worker thật. Không đổi migration/database, role/capability/authority hoặc session coordinator để làm UI demo hoạt động.

Trước khi sửa, đối chiếu code/API/test và trả lại danh sách: (1) lỗi UI đã xác nhận, (2) cải tiến UX đề xuất, (3) vấn đề chờ backend/quyền/dữ liệu, cùng file cụ thể. Không gọi My Staff hay dashboard là “chưa có”: cả hai đã có UI. Bản local dùng dữ liệu Development và authority V2 đang Unavailable nên có 503; phải giữ fail-closed.

Ưu tiên theo thứ tự:
1. Trang email-integration đang hoãn: không để người dùng nhập/lưu appPassword vào localStorage hoặc nhận thông báo thành công như cấu hình server. Giữ hai API email trả INTEGRATION_DEFERRED; không gọi mailbox thật.
2. Chi tiết công văn: hiển thị đầy đủ trường `details` đã có theo từng loại; không bịa tên người từ GUID hoặc bổ sung backend API chưa được thống nhất.
3. VI/EN: dictionary, nhãn/badge/error/date/pagination nhất quán trên form/detail/PDF/task/My Staff/report/dashboard/notification/partner. Giữ nguyên mã nghiệp vụ và contracts.
4. My Staff: tên người thực hiện từ dữ liệu được cấp, ngày VN, trạng thái dễ hiểu, bảng/empty/loading/errors. Đề xuất chọn nhân sự/lọc task/phân trang riêng nếu contract cho phép; không cấp quyền tài liệu từ quyền quản lý nhân sự.
5. Chuông thông báo: đề xuất lịch sử/paging vượt 20 nếu backend hỗ trợ; không tuyên bố đã có realtime khi đang polling.
6. Responsive, keyboard/focus/dialog, nội dung dài và branding/footer. Giữ attribution cần thiết theo license; không xóa hàng loạt template phụ thuộc dùng chung.

Giữ luật nghiệp vụ: 3 counter theo loại/năm dùng chung company/phòng; số mặc định 4 chữ số, chỉ tăng lên 5 chữ số khi vượt 9999; registration date hiện tại, issued date có thể quá khứ; sửa company/phòng giữ sequence; Cancelled restore trạng thái trước; reminder quá 7 ngày, thứ Hai 08:00 Việt Nam; go-live năm 2027. Admin không bypass quyền công văn. Metadata/menu/cached capabilities không là authority.

Giữ `useSessionIntent`, mutation ownership, version/idempotency và unknown-outcome recovery. Khi kết quả mutation chưa rõ, không phát sinh yêu cầu mới, không tự resend hoặc làm mất frozen request. Không chuyển lỗi 503/quyền thiếu thành empty success. Không lọc một page rồi gọi là tìm toàn database.

Nếu bạn chỉ là advisory MCP: trả lại phân tích, patch đề xuất và test cases; không nói đã đọc file, sửa code hoặc chạy tests nếu không có khả năng đó. Nếu có IDE/filesystem: làm một module mỗi lượt, giữ diff nhỏ, chạy test frontend liên quan/typecheck/lint; trước bàn giao cần full frontend suite/build và browser evidence phù hợp. Ghi rõ tests chưa chạy, nguồn fixture vs nguồn thật, và gate còn mở. Không push main hoặc thay việc đang chạy của người khác khi chưa được chủ dự án giao quyền.

Kết quả cần có: bảng vấn đề/file/ưu tiên; đề xuất sửa cụ thể; patch theo module; bằng chứng kiểm chứng; các câu hỏi thực sự cần contract mới. Không tự báo dự án hoàn thành/G7 nghiệm thu vì UI đẹp hơn.
