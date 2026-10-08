# J21 — Lọc assignee toàn bộ và phân trang My Staff độc lập

Trạng thái: **IMPLEMENTED_LOCAL / VERIFICATION_IN_PROGRESS**. API và UI local đã triển khai, targeted tests và review qua; gateway runtime/build/browser mới và TMS thật vẫn là gate riêng.

## Hiện trạng trước triển khai (bằng chứng của lỗi cần sửa)

`backend/services/document-service/Controllers/MyStaffController.cs` chỉ nhận `pageNumber`, `pageSize`, `includeTasks`; query khác bị 400. `workflows/business/document-service/Tasks/MyStaff.cs` phân trang staff và task bằng cùng page/size. Task được truy vấn trên toàn bộ ID nhân sự trong authority, nhưng UI `frontend/src/views/apps/tasks/MyStaff.tsx` lọc assignee trong **task page hiện tại**. Vì vậy có thể báo không có công việc dù công việc của người đó nằm ở trang khác. Name map từ staff page hiện tại cũng không bảo đảm có tên cho mọi task.

Authority kiểm actor active, ID/name/department, nhân sự trùng và giới hạn 2.000 người. Nguồn scope phải bao gồm nhân sự cấp dưới được phép xem theo yêu cầu tổ chức; DAS không suy quyền từ tên phòng, `parent=null`, hoặc role trong trình duyệt. EAP đang hoãn.

## Hợp đồng API đã triển khai local

Nhân sự: giữ GET `/api/v2/my-staff?pageNumber=1&pageSize=20&includeTasks=false` với DTO hiện hành. UI có `staffPage`, `staffPageSize` riêng. Không đổi hợp đồng cũ.

Công việc: thêm GET `/api/v2/my-staff/tasks`.

| Query | Quy định |
|---|---|
| `pageNumber`, `pageSize` | Quy tắc chung, phân trang task độc lập |
| `assigneeUserId` | Tùy chọn GUID khác rỗng; vắng mặt = mọi nhân sự trong managed scope; xuất hiện nhưng rỗng = 400 |

Không nhận `managerId`, `departmentId`, `userIds`, search/status/sort trong đợt này. Không thêm các bộ lọc chưa có semantics TMS.

Response 200 connected:

```json
{
  "success": true,
  "data": {
    "taskState": "Connected",
    "selectedAssignee": null,
    "tasks": {
      "items": [],
      "total": 0,
      "pageNumber": 1,
      "pageSize": 20
    }
  }
}
```

Task item: `taskId` opaque string 1–200, `assignee` là StaffMember đã xác minh (`userId`, `name`, `departmentId`, `departmentName`), `title` 1–2.000, `status` opaque string 1–100, `dueAt` ISO8601 UTC hoặc null. Các giới hạn mới là đề xuất, phải test adapter tương ứng. Chỉ ánh xạ những status mà frontend biết; status khác hiển thị trung tính với nhãn gốc được escape, không giả định Completed.

Khi chọn assignee, `selectedAssignee` phải đúng người được yêu cầu, kể cả ngoài staff page đang mở. Khi query không có assignee thì phải null. Không cần trả toàn bộ scope 2.000 người trong response task.

Degraded response khi authority đã xác minh nhưng TMS không phục vụ:

```json
{
  "success": true,
  "data": { "taskState": "TMS_UNAVAILABLE", "selectedAssignee": null, "tasks": null }
}
```

Chỉ dùng state `TMS_UNAVAILABLE`, `TMS_TIMEOUT`, `TMS_CONTRACT_INVALID`; **không** đưa `total=0/items=[]` khi chưa biết task count. Staff và tasks có request riêng nên UI vẫn giữ danh sách nhân sự đã xác minh và thông báo task chưa tải được. Khi scope rỗng đã được xác minh, trả Connected với page rỗng/total0, không gọi TMS.

## Luồng xử lý bắt buộc

1. Xác thực subject, kiểm query. Resolve **toàn bộ** managed scope, chạy `StaffAuthorityValidation` và actor active. Không chỉ kiểm staff của page hiện tại.
2. Nếu có assignee ngoài scope: 403 `ASSIGNEE_FORBIDDEN`, không gọi TMS, không tiết lộ tên/nhân sự có tồn tại hay không. Scope không phục vụ: 503 `STAFF_AUTHORITY_UNAVAILABLE`; mismatch: 503 `AUTHORITY_MISMATCH`.
3. Chọn set ID = assignee được phép hoặc toàn scope. Gọi connector lọc **set này trước pagination**. Deadline tối đa 15 giây như service hiện có, linked request cancellation; user hủy/unmount không biến thành thông báo lỗi TMS.
4. Adapter TMS phải bảo đảm stable ordering và total trong cùng lần truy vấn. DAS không tự đoán thứ tự từ taskId opaque. Hợp đồng DAS–TMS nội bộ phải ghi semantics này; API phía TMS chưa được cung cấp.
5. Kiểm metadata bằng page/size đã yêu cầu, total≥0, items≤size, total≥items; offset≥total thì items phải rỗng. Mọi assignee thuộc set cho phép, taskId không trùng trong page, task/title/status/date đúng shape. Sai → tasks null/TMS_CONTRACT_INVALID; không trả một phần task hợp lệ.
6. Resolve lại authority sau TMS trước trả dữ liệu; actor inactive/authority không có/mismatch → lỗi fail-closed. So sánh managed ID set; scope thay đổi trong lúc chờ → 409 `STAFF_SCOPE_CHANGED`, không trả tasks hoặc names từ scope cũ. Tên/phòng có thể cập nhật từ scope mới nếu ID set không đổi.
7. Map tên từ full scope mới đã xác minh. Không lấy tên actor từ claims tùy ý hoặc đoán từ GUID.

## Frontend

- Services thêm `getMyStaffTasks` riêng, decoder nghiêm ngặt; API staff cũ giữ nguyên. Nút chọn nhân sự set taskPage=1 và selected ID; đổi staffPage không thay taskPage/selected ID. “Bỏ lọc” bỏ assignee và về taskPage1.
- Hiển thị phân trang riêng trên hai bảng. Tên assignee trong task lấy từ item.assignee; không lệ thuộc staff page.
- Key request gồm session epoch, selected ID, task page/size. Mỗi effect có AbortController và guard response chủ sở hữu; đổi người nhanh A→B không được trả dữ liệu A vào bảng B.
- Scope 409 phải tải lại staff và task, giữ thao tác đọc an toàn; không tự gọi legacy. 401 xử lý session; 403 xóa task data/selection và thông báo không có quyền; 503 authority xóa cả dữ liệu thuộc scope đã mất; task degraded không xóa staff hợp lệ.
- “Không có công việc” chỉ khi Connected, total=0. Trang có0items nhưng total>0 hiển thị chuyển trang/kẹp trang đúng; không dùng thông báo global rỗng.
- Chọn assignee bằng keyboard, name trên row/button; tiêu đề lọc và nút bỏ lọc VI/EN; bộ lọc không chỉ dựa màu. Không xuất tên người không trong scope qua notification/tooltip.

## Điểm sửa sau khi hợp đồng được thống nhất

Controller/service: `MyStaffController.cs`, `workflows/business/document-service/Tasks/MyStaff.cs`; thêm query/DTO nội bộ hoặc file trong cùng Tasks. Không đổi public `ITmsConnector.ListAsync` gây vỡ các implementer nếu semantics cũ đủ: sử dụng allowed set được thu hẹp và document ordering. Nếu thêm API connector, cập nhật mọi adapter/test trong cùng changeset.

Gateway `backend/gateway/ocelot.json`: route **cụ thể** `/api/v2/my-staff/tasks`, GET/OPTIONS, port5002; route gốc hiện không có wildcard. Frontend: `services/das/reports-staff.ts`, `views/apps/tasks/MyStaff.tsx`, dictionary và tests; không sửa EAP adapter.

## Acceptance

| ID | Dữ liệu/trigger | Kết quả cần đạt |
|---|---|---|
| J21-A01 | Nhân sự A có task ở trang3 của tập chung; lọc A từ trang1 | Task của A xuất hiện trên trang1 của tập lọc; total chỉ tính A |
| J21-A02 | 45 staff/67 tasks, size20 | Staff page3 và task page2 giữ độc lập |
| J21-A03 | A thuộc scope nhưng nằm ngoài staff page hiện tại | Filter được, tên/phòng và selectedAssignee đúng |
| J21-A04 | ID ngoài scope / GUID rỗng / query managerId | 403 / 400 / 400; TMS không bị gọi |
| J21-A05 | Scope rỗng đã xác minh | Connected/tasks0; không gọi TMS |
| J21-A06 | TMS timeout/lỗi/malformed/wrongpage/duplicate/task ngoài scope | Degraded đúng state; không lộ task, không dựng0 |
| J21-A07 | Đổi chọn A→B trong lúc request A chậm | Chỉ dữ liệu B được render; A abort, spinner kết thúc đúng |
| J21-A08 | Authority bị thu hồi trong thời gian TMS chạy | Fail-closed sau resolve lại; không trả payload cũ |
| J21-A09 | Actor không hoạt động/token hết hạn | 403/401; dữ liệu session cũ bị xóa |
| J21-A10 | Unknown status/date null/title dài/XSS text | Trung tính/nhãn không có hạn/wrap/escape; keyboard và VI/EN đúng |
| J21-A11 | Backend chỉ đang hỗ trợ route cũ | UI mới không được phát query assignee vào route cũ |
| J21-A12 | Request đã hủy / gateway route mới | Không tự báo timeout sau unmount; GET qua gateway đúng service |

Không đánh dấu J21 hoàn thành positive TMS bằng test fixture; tách kết quả contract/component khỏi UAT integration.
