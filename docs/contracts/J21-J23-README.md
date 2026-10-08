# Thiết kế API bổ sung J21–J23

Ngày: 08/10/2026. Trạng thái: **IMPLEMENTING — người dùng yêu cầu triển khai ngày08/10/2026**. J21 đã có code local/review; J22–J23 đang triển khai. Chưa kết luận gateway/browser/production acceptance.

Phạm vi lượt thiết kế trước là hợp đồng API và tiêu chí nghiệm thu. Lượt hiện tại thực hiện các hợp đồng đó; theo dõi bằng [checkpoint triển khai](../J21-J23-IMPLEMENTATION-CHECKPOINT-20261008.md). Code local không thay nghiệm thu TMS/authority/dữ liệu khách hàng thật; chỉ đổi trạng thái ma trận khi có bằng chứng đúng gate.

## Đọc và triển khai

1. [J21 — My Staff](J21-MY-STAFF.md): lọc người được giao trên toàn bộ tập công việc được phép xem, phân trang nhân sự/công việc độc lập.
2. [J22 — Danh mục ngừng hoạt động](J22-INACTIVE-CATALOGS.md): danh sách quản trị có trạng thái hoạt động, khôi phục bằng PUT có version, giữ lookup tạo công văn active-only.
3. [J23 — Lịch sử](J23-AUDIT-HISTORY.md): timeline đối tác, deep link, lịch sử hủy/khôi phục công văn từ bằng chứng đã lưu.
4. [OpenAPI dự thảo](J21-J23.openapi.json): các endpoint đọc đề xuất, không phải mô tả API đang chạy.
5. [Kế hoạch triển khai](../superpowers/plans/2026-10-08-j21-j23-contracts.md): file cần sửa, ca kiểm thử và thứ tự bàn giao.

## Các quyết định đề xuất

| Quyết định | Lý do |
|---|---|
| J21 thêm GET `/api/v2/my-staff/tasks` | Không thay đổi semantics endpoint My Staff cũ hoặc gửi query hiện chưa được chấp nhận |
| Danh sách nhân sự dùng endpoint cũ với `includeTasks=false` | Tách page state mà không cần tạo thêm endpoint nhân sự |
| J22 thêm GET `/api/v2/admin/catalogs` và GET `/options` dưới route này | Phân biệt browse quản trị với lookup nghiệp vụ; quyền lấy từ backend |
| J23 đối tác dùng `CatalogManage` | Chính sách sẵn có, không tự cấp thêm quyền đọc lịch sử nhân sự/đối tác |
| J23 công văn dùng authority đọc công văn hiện hành | Người sửa, quản trị danh mục hoặc trưởng phòng không mặc nhiên được đọc mọi công văn |
| Timeline dùng page + `throughVersion` chuỗi số thập phân | Tập audit bất biến; tránh lặp dòng khi có sự kiện mới và tránh mất chính xác Int64 ở JavaScript |
| Partner timeline đợt đầu chỉ có metadata | Database hiện chưa lưu before/after; không dựng thay đổi cũ từ bản ghi hiện tại |
| Lịch sử hủy dùng `DocumentEditAudit` | `DocumentCancellation` chỉ giữ chu kỳ gần nhất; không đủ cho timeline nhiều chu kỳ |

Đây là lựa chọn kỹ thuật để review. Không coi đề xuất quyền/DTO/route là quyết định nghiệp vụ mới đã được mentor xác nhận. Nếu cần mở rộng người đọc timeline, snapshot trường đối tác hoặc API TMS thật, phải thống nhất riêng trước khi bật.

## Quy tắc chung

- Chỉ lấy subject từ token đã xác thực; không nhận actor/manager ID từ query. Subject phải là GUID khác rỗng. Mỗi request xác minh authority, actor active và phạm vi dữ liệu; dữ liệu fixture chỉ dùng trong test.
- Query phân biệt hoa thường, có allowlist, mỗi khóa đúng một giá trị. Khóa lạ, giá trị trống ở tham số tùy chọn đã xuất hiện, GUID rỗng, enum sai, số ngoài khoảng đều trả 400. Không bỏ qua query sai hoặc tự đổi sang phạm vi rộng hơn.
- `pageNumber`: 1–1.000.000; `pageSize`: 1–100; mặc định 1/20. Tính offset bằng kiểu số đủ rộng, kiểm tra overflow trước truy vấn.
- Lọc theo quyền và điều kiện nghiệp vụ **trước** Count/Skip/Take. Total mô tả cùng tập lọc với items. Một response count/items phải đọc trên một snapshot DB nhất quán; không mở transaction qua lời gọi TMS hoặc authority từ xa.
- Trang ngoài phạm vi hợp lệ trả items rỗng, giữ metadata của page đã yêu cầu và total chính xác. UI dùng effect mới của trang được kẹp; không dùng request cũ để tải bù.
- Response đọc đặt `Cache-Control: no-store`. Không cache dữ liệu cũ qua tài khoản, đăng xuất, thay quyền hoặc đổi phạm vi. Không log token, nội dung PDF hoặc cấu hình local.
- Giữ envelope hiện hành theo từng service; không ép API cũ đổi shape. Các schema OpenAPI mới chỉ cho endpoint bổ sung. Error code thuộc đề xuất, còn code cũ giữ nguyên.
- Tên actor/nhân sự chỉ lấy từ nguồn xác minh. GUID có thể hiển thị bằng nhãn “ID”; không suy tên, phòng hoặc thời điểm từ GUID/chuỗi ghi chú.
- Thời gian wire ISO8601 UTC `Z`; UI hiển thị theo locale, với nhãn múi giờ khi cần. Lịch nhắc vẫn >7 ngày, thứ Hai 08:00 Việt Nam.
- Phản hồi sai schema/scope từ upstream: lỗi 502/503 theo tài liệu từng module; không coi là danh sách rỗng thành công. UI có thông báo lỗi, tải lại; không tự retry mutation.
- Không tạo FK tới DB của service khác. Không làm EAP/OCR, không nối TMS/SMTP thật khi chưa có cấu hình/hợp đồng được xác nhận.

## Giới hạn và rollout

Thiết kế J21 có thể viết test với adapter fixture hợp lệ. Nghiệm thu positive với TMS thật vẫn cần phía TMS cung cấp filter trước phân trang, total, ID mapping và paging ổn định. Không tải mọi task không thuộc quyền rồi lọc ở client.

J22 và partner audit có thể triển khai trên database hiện có sau khi thống nhất API. Document lifecycle history cần parser allowlist cho `ChangesJson` và test dữ liệu cũ; không cần tự thêm cột để suy diễn lịch sử. Chỉ cân nhắc index/migration sau kiểm tra query plan SQL và index hiện có.

Triển khai backend/gateway trước; contract tests qua gateway; sau đó frontend với regression quyền/session/request ownership. Feature chưa được backend hỗ trợ phải giữ trạng thái chưa sẵn sàng. Không fallback sang API legacy. Không commit/push trong lượt thiết kế này.
