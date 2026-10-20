# J22 — Browse danh mục ngừng hoạt động và kích hoạt lại

Trạng thái ngày09/10/2026: **IMPLEMENTED_LOCAL / BROWSER_VERIFIED**. Backend/options, frontend quản trị và route gateway đã triển khai; kiểm thử HTTP/SQLite, SQL Server, component và browser local đã đạt. Đã thao tác ngừng dùng/kích hoạt lại bằng Enter trên cùng ID/code trong database kiểm thử. Quyền/capability và dữ liệu production vẫn cần nghiệm thu riêng.

## Hiện trạng trước triển khai

`CatalogService.GetAsync` ở `backend/services/document-service/Services/CatalogService.cs` trả active-only. GET by ID có thể đọc inactive để giải thích công văn lịch sử. PUT quản trị hiện hỗ trợ `name`, `sortOrder`, `isActive`, `version` và optimistic concurrency; thiếu danh sách inactive để tìm và kích hoạt lại. Frontend `catalogApi.getGroup` cố ý từ chối dòng inactive, phải giữ validator này.

Sáu nhóm: companies, methods, documentTypes, internalTypes, sensitivity, categories. Chỉ methods, documentTypes, internalTypes, categories được sửa. companies/sensitivity là nhóm cố định, không tự mở quyền sửa để phục vụ browse. Distribution targets riêng, liên quan mapping EAP; không mở rộng trong J22.

## Endpoint mới

GET `/api/v2/admin/catalogs/options`, authenticated subject hợp lệ, trả:

```json
{"success":true,"data":{"canManage":false},"message":null,"errors":[],"traceId":"request-trace-id"}
```

`canManage` lấy từ policy/claims backend đã xác thực, cùng điều kiện actor mà create/update thực thi; không lấy localStorage role. Không cấp capability mới. Client signed in thiếu quyền có thể nhận options false; endpoint list bên dưới vẫn trả403. Nếu sau này authority quyền không phục vụ, trả503 thay vì false giả thành công.

GET `/api/v2/admin/catalogs`, policy `CatalogManage`:

| Query | Quy định |
|---|---|
| `group` | Bắt buộc, đúng một trong sáu nhóm, không comma list |
| `activity` | Active (mặc định), Inactive, All; enum phân biệt hoa thường |
| `searchTerm` | Tùy chọn, trim; 1–200 sau trim, không wildcard do client quy định |
| `pageNumber`, `pageSize` | 1–1.000.000/1–100, mặc định1/20 |

Response giữ envelope CatalogsController: `{success:true,data:{group,activity,items,totalCount,pageNumber,pageSize,canEditGroup},message:null,errors:[],traceId}`. Lỗi giữ `{success:false,data:null,message,errors:[{field,code,message}],traceId}`, không đổi sang shape error của MyStaff/DocumentsV2.

Item giữ DTO hiện hành: GUID id; group; code1–64; name1–200; sortOrder0–10000; isActive bool; version số nguyên dương ≤Number.MAX_SAFE_INTEGER. Nếu dữ liệu lưu vượt giới hạn wire hoặc sai nhóm/shape, lỗi dependency có code hợp lệ, không giảm/làm tròn version.

`canEditGroup` chỉ true khi subject có CatalogManage **và** group thuộc bốn nhóm editable. Danh sách fixed group vẫn đọc được dưới quyền quản trị nhưng editor disabled, không phát PUT. Các mã nhóm/code không đổi qua update.

Code lỗi đọc đề xuất: 400 `INVALID_ADMIN_CATALOG_QUERY`, 401 challenge, 403 `CATALOG_MANAGE_FORBIDDEN` (authorization middleware có thể chỉ trả challenge/forbid rỗng), 503 `CATALOG_DEPENDENCY_UNAVAILABLE` hoặc `CATALOG_DATA_INVALID` nếu dữ liệu lưu không đáp ứng DTO. Các code write hiện có như `FIXED_GROUP`, `DUPLICATE_CODE`, `VERSION_CONFLICT` giữ nguyên.

## Query/consistency

- Filter group, activity và searchName/code trước count/page; search không phân biệt hoa thường theo collation SQL đã chọn trong dự án, SQLite test phải chỉ rõ nếu khác. Escape LIKE `%/_/[]` khi dùng Like để chuỗi nhập không thành wildcard; parameterized query.
- Stable order SortOrder ASC, Code ASC, Id ASC. Count và item page từ cùng snapshot đọc DB; dùng capability isolation SQL hiện có, không vô cớ bật RCSI cả database. Với SQLite test dùng transaction tương thích.
- Include inactive chỉ tồn tại trên route quản trị. GET `/api/v2/catalogs?groups=...`, document options và business validation vẫn active-only. Không đổi danh sách lookup toàn hệ thống thành All.
- Trang cuối co về0: response empty, total0 và page request; UI kẹp về page1, clear result key, effect trang mới là owner. Guard abort khi đổi group/keyword, không tải bù bằng effect đã hết hạn.
- Code uniqueness tính cả inactive. Tạo trùng inactive trả409 DUPLICATE_CODE, hướng dẫn admin tìm/kích hoạt lại; không tạo ID mới thay lịch sử.

## Kích hoạt lại bằng API hiện có

GET by ID để lấy phiên bản mới nhất; PUT `/api/v2/admin/catalogs/{id}` với `{name,sortOrder,isActive:true,version}`. Phải giữ nguyên tên/thứ tự nếu người dùng chỉ chọn kích hoạt lại; không lấy default của form tạo. Backend increment version và ghi audit trong cùng SaveChanges/transaction như hiện hành.

Version conflict409: giữ bản nháp, khóa lưu đến reload thành công; không gửi PUT tự động lần2. 401/403: thu hồi canManage ngay và không mở lại editor bằng đóng/mở dialog. 500/mạng sau PUT: không biết kết quả, buộc reload trước mutation tiếp theo; không báo chưa lưu chắc chắn. Ref guard đồng bộ chặn double Enter/click. Không đổi policy để làm positive test trên account thiếu capability.

## UI cần triển khai

`BusinessCatalogs.tsx`: lấy options authoritative; admin mới thấy bộ lọc hoạt động và danh sách quản trị. Người đọc thường vẫn dùng active-only view hiện có. Chọn activity/group/search đưa page về1. Row inactive badge rõ; nút Kích hoạt lại chỉ trên editable group và canManage. Disabled editor có giải thích; không dùng màu đơn độc.

Lịch sử công văn có ID inactive vẫn render tên đã lưu/lookup by ID có nhãn ngừng hoạt động; không ép đổi sang giá trị active khác. Tạo công văn mới không cho chọn inactive. Update công văn cũ tuân theo validator nghiệp vụ hiện có, J22 không tự đổi semantics giữ giá trị lịch sử.

Services thêm `getAdminPage` và `getOptions` tách `getGroup`. Decoder kiểm group/activity, ID và code không trùng, total/page/size đúng, active predicate và `canEditGroup`; sai502. Backend `CatalogsController`, `CatalogService`; gateway route gốc admin hiện chỉ POST, thêm GET/OPTIONS và route options cụ thể GET/OPTIONS. Không đổi PUT semantics.

## Acceptance

| ID | Ca | Kết quả |
|---|---|---|
| J22-A01 | Có25active/7inactive cùng group, size20 | Active/Inactive/All có đúng total25/7/32 và pages |
| J22-A02 | Reader gọi admin list trực tiếp | 403, không trả inactive; optionsfalse; lookup thường chỉactive |
| J22-A03 | Admin browse companies/sensitivity | Đọc được; canEditGroupfalse; PUT vẫn FIXED_GROUP |
| J22-A04 | Chọn kích hoạt lại inactive, version đúng | Cùng ID/code; active true, version+1, đúng một audit |
| J22-A05 | Hai admin kích hoạt cùng version | Một thành công; một409; không mất sửa đổi người khác |
| J22-A06 | Tạo code trùnginactive / query lạ / activity rỗng | 409 /400 /400, không thay dữ liệu |
| J22-A07 | Mất401/403 khi reload editor | Thu hồi quyền, đóng/mở không khôi phục quyền giả |
| J22-A08 | NhómA chậm, đổi nhómB/từ khóa nhanh | ResponseA không ghi lênB; abort đúng, không spinner kẹt |
| J22-A09 | Xóa/đổi trạng thái làm page cuối mất hết | Owner mới tải page hợp lệ; không stale rows |
| J22-A10 | Công văn lịch sử dùng inactive | Tên vẫn đọc được; không tự sửa FK/number; create lookup không cóinactive |
| J22-A11 | Search `%_[]` / Unicode / SQL text | Chuỗi literal được tìm an toàn; không trở thành wildcard/SQL injection |
| J22-A12 | Tab/Enter/Escape/long name/VIEN/mobile | Nút và filter có tên, focus đúng; không tràn page |

Index đề xuất `(Group,IsActive,SortOrder,Code,Id)` phải đối chiếu index hiện có và query plan trước migration; search contains có thể scan, không tuyên bố index này giải quyết mọi tìm kiếm. Không thay đổi dữ liệu khách hàng để thử hiệu năng.
