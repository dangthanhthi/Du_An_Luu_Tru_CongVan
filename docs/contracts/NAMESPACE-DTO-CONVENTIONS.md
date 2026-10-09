# Quy ước namespace và DTO của nguồn DAS hiện tại

Ngày rà soát: 09/10/2026. Áp dụng cho nguồn phát triển `DAS-Collaboration` và việc ghép backend chung của thành viên 1 và 3. Người dùng đã chốt giữ cách đặt tên dễ hiểu hiện tại; không chờ một quyết định đổi namespace toàn bộ. Các tính năng Auth ngoài phạm vi vẫn được bảo toàn. Chưa commit/push thay đổi lượt này.

## Namespace và assembly sở hữu

| Module | Namespace giữ nguyên | Assembly sở hữu |
|---|---|---|
| Database/model, DTO, Numbering và workflow công văn | `DocumentService` | `DocumentService` |
| User/Role/Department và DbContext auth | `AuthService` | `AuthService` |
| Entity projection tổ chức đã tồn tại | `AuthService.Organization` | `AuthService` |
| Đối tác và DbContext | `PartnerService` | `PartnerService` |
| File entity, context, DTO | `FilesService.Models.Entities`, `FilesService.Data`, `FilesService.Models.DTOs` | Project `FileService.API.csproj` |
| Notification entity/context | `NotificationService.Models`, `NotificationService.Data` | `NotificationService` |
| Email entity/context | `EmailWorkerService.Models`, `EmailWorkerService.Data` | `EmailWorkerService` |
| Hợp đồng PDF liên service | `Das.PdfProtocol` | Thư viện `PdfProtocol` |

Nhánh Git và tên folder không tạo namespace mới. `numbering/DocumentRegistrationWriter.cs` vẫn thuộc `DocumentService`, không đổi thành `Numbering.DocumentRegistrationWriter`. `database` không phải namespace hoặc một database dùng chung. Không thêm tiền tố `Das.*` cho tất cả class trong lượt ghép này vì sẽ đổi tham chiếu của các service đang có.

Các kiểu cùng tên ở các namespace khác nhau không tự là xung đột. Ví dụ `AuthService.ApiResponse<T>` và `FilesService.Models.DTOs.ApiResponse<T>` có chủ sở hữu và consumer riêng; không ép thành một class chung khi chưa chuyển hết các consumer. Trong một assembly, mỗi tên đầy đủ chỉ được khai báo một lần, trừ các phần `partial` hợp lệ.

## Đặt tên và nơi sửa định nghĩa

- Class/record/enum/property/method C# dùng PascalCase; biến và tham số dùng camelCase. Interface dùng tiền tố `I`; method bất đồng bộ giữ hậu tố `Async` đang có.
- Entity là dữ liệu lưu trữ, đặt trong `Models/Entities` hoặc thư mục Domain của service sở hữu. Request/response/draft đặt trong `Models/DTOs` hoặc Contracts của service. Không đặt bản sao DTO trong module Numbering.
- File đơn kiểu mới ưu tiên trùng tên kiểu. File `*Contracts.cs` hoặc `*Entities.cs` được phép gom các khai báo cùng nghiệp vụ; đây là quy ước C# của nguồn hiện tại, không áp dụng PSR-4/CakePHP cho các file C#.
- Giữ tên bảng/cột/FK/index hiện tại. Việc chuyển database sang snake_case là thay đổi schema cần nhóm chốt riêng; không đổi bằng refactor namespace/DTO.
- Giữ loại nullable, thứ tự tham số của positional record, default values và JSON attributes. Không đổi `string?` thành `string` hoặc thay `long` bằng `int` để ghép nhanh.

| Kiểu/hợp đồng | Nguồn định nghĩa duy nhất |
|---|---|
| `Document`, counter và lịch sử legacy | `backend/services/document-service/Models/Entities/Documents.cs` |
| `DocumentRegistration`, `RegistrationRequest`, `DocumentOutboxEvent` | `backend/services/document-service/Models/Entities/DocumentV2Persistence.cs` |
| Năm entity task/notification/reminder ledger | `backend/services/document-service/Models/Entities/DeliveryEntities.cs` |
| `V2RegistrationDraft`, `RegistrationIdentity`, `DocumentRegistrationRuleException` | `backend/services/document-service/Models/DTOs/RegistrationContracts.cs` |
| HTTP register/edit/status, write result, row/detail/page | `backend/services/document-service/Models/DTOs/V2HttpContracts.cs` |
| Snapshot chi tiết công văn trả về, không mang EF navigation | `backend/services/document-service/Models/DTOs/V2ReadContracts.cs` |
| Chi tiết từng loại công văn đầu vào | `backend/services/document-service/Models/DTOs/V2KindDetailsContracts.cs` |
| Ý định thêm/bỏ liên kết và phạm vi đã xác minh | `backend/services/document-service/Models/DTOs/V2RelationContracts.cs` |
| Metadata tối thiểu của Partner/Files và client interfaces | `backend/services/document-service/Models/DTOs/ServiceClientContracts.cs` |
| Metadata file do Files trả ra | `backend/services/files-service/Models/DTOs/ManagedFileInfo.cs` |
| Prepare/receipt/operation PDF liên service | `backend/shared/PdfProtocol/Contracts.cs` |
| Kiểu TypeScript cho form/detail | `frontend/src/types/das/document-v2.ts` |

DTO ở DocumentService dùng để đọc HTTP từ Partner/Files là projection của consumer, không phải bản sao EF entity và không tạo ProjectReference tới service khác. `FileMetadataDto` là projection của `ManagedFileInfo`; các trường ID/tên/size/content type/state/hash/canAttach/canDownload phải cùng ý nghĩa, còn thứ tự constructor có thể khác vì JSON map theo tên trường.

## Kiểu dữ liệu và JSON

| Ý nghĩa | C# | HTTP JSON / TypeScript |
|---|---|---|
| ID | `Guid`, hoặc `Guid?` khi tùy chọn | Chuỗi GUID; trường bắt buộc không nhận GUID rỗng |
| Ngày nghiệp vụ | `DateOnly`, `DateOnly?` | Chuỗi `YYYY-MM-DD`, hoặc null khi được phép |
| Thời điểm | `DateTimeOffset` hoặc DateTime UTC đã xác minh | ISO8601; không suy timezone cho dữ liệu cũ chưa rõ nguồn |
| Version mutation hiện hành | `long`, dương | JSON number; client yêu cầu `Number.isSafeInteger` và dương |
| Số thứ tự | `int` | Không nhận từ request đăng ký; do server cấp |
| Size file | `long` | JSON number; PDF tối đa 26.214.400 bytes, nằm trong miền số nguyên an toàn |
| Nơi nhận/liên kết | `IReadOnlyList<Guid>?` | Array GUID hoặc không có/null theo DTO; không truyền tên thay ID |

Member C# giữ PascalCase; HTTP controller dùng JSON web defaults với tên camelCase, ví dụ `ExpectedVersion` → `expectedVersion`. JSON body khác với tên cột trong database. Không tự đổi tên trường wire khi đổi tên file nguồn.

Version mutation hiện hành vẫn là number. Không thể giả định mọi giá trị Int64 đều biểu diễn chính xác trong JavaScript: client hiện từ chối số không an toàn. Việc chuyển version endpoint hiện hành sang chuỗi cần thay contract producer/consumer cùng lúc. `throughVersion` và event `version` dạng chuỗi trong endpoint J23 đã triển khai là hợp đồng đọc riêng, không thay version number của mutation hiện hành.

## Đăng ký, sửa và chuyển trạng thái

| HTTP DTO | Trường bắt buộc / hành vi |
|---|---|
| `RegisterDocumentRequest` | Kind, CompanyCode, Subject, OriginatorUserId, OwnerDepartmentId; Sensitivity mặc định Normal; IssuedDate/Remark/Details/RelatedDocumentIds tùy chọn theo loại |
| `EditDocumentRequest` | ExpectedVersion, CompanyCode, Subject, OriginatorUserId, OwnerDepartmentId, Sensitivity; Details/Relations tùy chọn |
| `ChangeDocumentStatusRequest` | ExpectedVersion, Action; Reason dùng khi Cancel. Restore về trạng thái trước hủy do server quyết định |
| `V2WriteResult` | Id, RegistrationNumber, Version, Status |

`Idempotency-Key` là header đăng ký, không thêm vào draft body. Inputter lấy từ subject đã xác thực. Không nhận sequence, registration date, registered-at, phiên bản khởi tạo, inputter hoặc tên phòng do browser cấp. Issued Date có thể quá khứ; Registration Date do clock server xác định theo Việt Nam.

HTTP kind dùng `Incoming`, `Outgoing`, `Internal`; mapper hiện hành cũng chấp nhận các dạng INCOMING/OUTGOING/INTERNAL và chuyển về uppercase cho persistence. Không sử dụng HTTP kind để so sánh trực tiếp với DbContext mà bỏ mapper. Trạng thái V2: `InProgress`, `Distributed`, `Cancelled`; không gộp vocabulary legacy Draft/Signed vào V2.

`V2KindDetailsDraft` là dữ liệu ghi. Các tên snapshot do server xác minh và lưu không thuộc draft: senderNameSnapshot, methodNameSnapshot, documentTypeNameSnapshot, categoryNameSnapshot. Đã tách TypeScript `KindDetails` (ghi) khỏi `ReadKindDetails` (đọc); recipientPartnerIds/distributionTargetIds chỉ là đầu vào. Danh sách nơi nhận đọc nằm ở `V2DocumentDetail.Recipients`, không giả định entity detail trả các input arrays này.

`V2DocumentDetail.Details` dùng `V2KindDetailsView`, là record đọc độc lập với EF entity. `V2DocumentQueries` ánh xạ từng trường từ `DocumentKindDetails`; không có navigation `Document`. Giữ nguyên 14 trường JSON cũ, gồm `documentId`, các ngày/ID/code và tên snapshot, số hợp đồng/nơi nhận khác/ghi chú; null vẫn là null. Danh sách nơi nhận vẫn ở `Recipients`, không thêm các input arrays vào view. Chuyển từ entity sang view không đổi HTTP JSON, nhưng C# consumer phải dùng kiểu mới; ghép `V2ReadContracts.cs`, `V2HttpContracts.cs` và mapper query cùng phiên bản.

## Hợp đồng quyền nội bộ

`RegistrationIdentity`, `V2EditorActor`, `V2EditTarget`, `V2ReferenceSet`, `V2RelationScope` và các authority response là dữ liệu nội bộ đã được xác minh; không phải request JSON để UI tự chọn quyền. `DocumentActor` từ claims phục vụ đường legacy; không thay `V2EditorActor` hoặc grant của authority V2 bằng role JWT legacy.

`IDocumentV2Authority` vẫn thuộc boundary server trong `Services/Authorization`. Lượt này không implement adapter EAP. Khi chưa có authority được cấu hình, lỗi unavailable là kết quả đúng; không dùng role Admin để bỏ qua scope và làm demo chạy.

## Envelope và lỗi

Giữ envelope riêng từng endpoint/service. V2 Document trả `{success:true,data:...}`; lỗi nghiệp vụ thường `{success:false,code:...}`. PDF có các lỗi `{code:...}`; Auth/Files có ApiResponse riêng. Không đồng nhất tất cả thành một `ApiResponse` bằng cách đổi wire field mà chưa cập nhật consumer.

Các lỗi cần phân biệt: 400 dữ liệu/yêu cầu không hợp lệ; 401 subject/phiên không hợp lệ; 403 quyền không đủ; 404 resource không tồn tại hoặc ngoài phạm vi theo endpoint; 409 conflict/version/idempotency; 423 PDF chưa sẵn sàng; 502 response upstream không hợp lệ; 503 dependency/authority unavailable. Consumer phải kiểm schema và HTTP status, không coi error/degraded là dữ liệu rỗng thành công.

## Quyết định áp dụng và ranh giới phối hợp

1. Dùng quy ước C# hiện tại, tên dễ hiểu theo quyết định người dùng. Không chuyển namespace/schema sang convention CakePHP hoặc snake_case trong lượt này.
2. Người phụ trách Auth quyết định bảo toàn các tính năng User soft delete/password-reset hiện có trên official main; không thay entity Auth từ gói nguồn mà làm mất các navigation/tính năng đó.
3. Thành viên 1 và 3 dùng chung nguồn local: V2DocumentRelations đã có và compile cùng assembly với writer. Không chờ cung cấp lại module này; TMS payload/auth/ID mapping và EAP adapter vẫn thuộc tích hợp bên ngoài đang hoãn.
4. J23 đã triển khai local: Document DTO ở V2HistoryContracts.cs; Partner DTO cùng PartnerAuditQuery.cs. Không tạo class trùng ở shared-contracts và workflow; wire history Int64 string/UTCZ khác với version number mutation legacy.

Xem [dependency](MEMBER1-DEPENDENCY-MAP.md), [phương án ghép TV1+TV3](MEMBER1-MEMBER3-INTEGRATION-PLAN.md) và [bảng từng file](MEMBER1-MEMBER3-FILE-MAP.md) trước khi thay thế nguồn trong backend chính.


## DTO bổ sung J21–J23

- J21 tasks dùng `selectedAssigneeUserId` và paging tasks riêng; lọc trên server trước total/items. Danh sách staff endpoint cũ dùng includeTasks=false, không gửi assignee vào endpoint cũ.
- J22 admin DTO `CatalogAdminPage`/query ở Services/CatalogAdminQuery.cs, `CatalogItemDto` ở CatalogService.cs. Đây là vị trí canonical hiện có, chưa chuyển thành shared library. Admin response phải giữ group/activity/page/total/capability; lookup công văn vẫn active-only.
- J23 Document `V2LifecycleHistoryItem`/`V2LifecycleHistoryPage` ở Models/DTOs/V2HistoryContracts.cs, namespace DocumentService. Page có DocumentId/ThroughVersion/Items/TotalCount/PageNumber/PageSize và coverage. Partner `PartnerAuditItem`/`PartnerAuditPage` nằm cùng file Application/PartnerAuditQuery.cs, namespace PartnerService.
- Version string là số thập phân canonical, không leading zero/dấu cộng/phần thập phân; frontend dùng BigInt khi so sánh. Timestamp wire UTC kết thúc Z. Không lấy tên actor từ GUID và không dựng before/after Partner từ bản ghi hiện tại.
