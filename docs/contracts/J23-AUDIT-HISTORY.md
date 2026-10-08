# J23 — Timeline đối tác, deep link và lịch sử hủy công văn

Trạng thái: **PROPOSED / CONTRACT_REQUIRED**. Chỉ đề xuất endpoint đọc; không thêm snapshot/migration trong lượt này.

## Bằng chứng hiện có trong database

`PartnerAudit` trong `backend/services/partner-service/Infrastructure/Persistence/PartnerDbContext.cs` có Id, PartnerId, ActorUserId, Action, Version, CreatedAt. Unique `(PartnerId,Version)`, FK restrict. `PartnerBusinessService.SaveAsync` ghi entity và audit trong cùng transaction. **Không có before/after hoặc thay đổi từng trường.** Timeline đợt1 chỉ công bố các metadata này.

`DocumentEditAudit`: Id, DocumentId, ActorUserId, Version, ChangedAt, ChangesJson. `V2DocumentLifecycle` ghi Status before/after và snapshot Cancellation trong ChangesJson mỗi lần hủy/khôi phục. `DocumentCancellation` riêng chỉ lưu chu kỳ gần nhất. `DocumentStatusHistory` lưu status/actor/time/note nhưng không có Registration version, cần xử lý lịch sử legacy riêng. DTO detail hiện chưa trả cancel reason/history.

Không dựng actorName từ GUID, không dùng `lastModifierUserId` làm actor mọi event, không dùng thời gian file hoặc ngày đăng ký làm thời gian hủy. Không suy lý do khôi phục từ Note nếu chưa có schema chứng minh.

## Phân trang audit chung

GET đầu với `pageNumber=1&pageSize=20`: backend chụp version hiện tại của entity làm `throughVersion`, rồi query audit `Version <= throughVersion` trong cùng DB read snapshot. Response trả throughVersion dưới dạng **chuỗi thập phân Int64 dương**.

Trang sau gửi lại throughVersion; event mới có version cao hơn không làm xê dịch các trang của tập đang đọc. Bấm tải lại vềpage1 và bỏ watermark để lấy sự kiện mới. throughVersion phải1–9223372036854775807, không có dấu/+0/leading zero; giá trị lớn hơn version entity hiện tại trả400 `INVALID_HISTORY_QUERY`. Chỉ lower/equal version chứ không chứng minh quyền; mọi request vẫn xác minh quyền mới.

Order theo cột số Version DESC, Id ASC trong database; không sort chuỗi version ở client (chuỗi "10" không đứng trước "9" khi dùng thứ tự từ điển). Lọc, count, items trên cùng watermark. Không đổi hoặc xóa audit đã phát hành qua API này. Không có event trùng ID hoặc version trong cùng entity khi schema yêu cầu unique. Version thiếu giữa các event là hợp lệ (một số thay đổi có kênh audit riêng); không tự chèn event lấp khoảng trống.

Timestamp UTC ISO8601; DateTime lưu legacy chưa biết timezone không tự gắn Z: trả history dependency error cho dòng đó hoặc nguồn chưa xác minh theo một contract riêng đã review. Không trả thành công bằng cách bỏ mất event lỗi.

## Đối tác

GET `/api/partners/{partnerId}/audit`, policy **CatalogManage** và GUID subject hợp lệ. Query chỉ pageNumber,pageSize,throughVersion. Dùng IgnoreQueryFilters cho existence của partner để người quản trị được đọc lịch sử soft-delete. Missing404 `PARTNER_NOT_FOUND`; không có quyền403 trước existence check để tránh dò ID. Không purge/hard delete.

Envelope success theo partner service: `{success:true,data:{partnerId,throughVersion,items,totalCount,pageNumber,pageSize}}`. Lỗi giữ `{success:false,data:null,message,errors:[{field,code,message}],traceId}` như PartnersController hiện hành.

Item: id, actorUserId, action (`Create`,`Update`,`Delete`,`Restore`), version chuỗi, occurredAt UTC, `changesAvailability:"NotRecorded"`. Không có fabricated changedFields. Nếu audit chứa action lạ cần schema evolution trước; không gán Update mặc định.

UI timeline ghi “Đã tạo / Đã sửa / Đã xóa mềm / Đã khôi phục”, thời gian và ID actor; nhãn “Không lưu chi tiết thay đổi trường trong dữ liệu hiện có”. Không lấy tên/current contact info rồi ghép thành snapshot của event cũ. Nội dung định danh được escape, không render HTML từ dữ liệu audit.

Deep link frontend mới đề xuất `/[lang]/apps/partners/[id]?tab=history` (hiện chỉ có list route). Chỉ dùng ID GUID đã decode, lang trong danh sách ứng dụng. Link từ row mở detail/history; direct URL kiểm đăng nhập/quyền/missing/softdeleted. Route identity đổiA→B hủy fetchA; không giữ timelineA. Không làm partner card giả bằng dữ liệu list cũ khi detail request lỗi.

API byID hiện dùng IgnoreQueryFilters; việc list audit thêm quyền không tự đổi semantics đọc partner byID hiện có. UI history có thể cần nút theo optionscanManage; backend vẫn là điểm kiểm quyền cuối.

## Công văn — chỉ lịch sử lifecycle trong đợt đầu

GET `/api/v2/documents/{documentId}/history`. Query như trên. Authorization dùng `IDocumentV2Authority.ReadAsync`, `V2HttpActor.Verify` và membership `ReadableDocumentIds` cùng convention detail. Không dùng CatalogManage hoặc chỉ quyền edit để mở read. Actorinactive403, authorityunavailable503. ID không đọc được hoặc không tồn tại cùng404 `DOCUMENT_NOT_FOUND`.

Đọc registration/document để kiểm kind nhất quán và lấy version. Source **DocumentEditAudit** với payload đã được parser allowlist xác minh là thay đổi lifecycle; parser lọc loại sự kiện trước count/paging. Envelope `{success:true,data:{documentId,throughVersion,items,totalCount,pageNumber,pageSize,coverage}}`.

Item: id, actorUserId, version chuỗi, occurredAt, action Distribute/Cancel/Restore, fromStatus/toStatus enum InProgress/Distributed/Cancelled, `cancellationReason` string1–4000 hoặc null. Cancel reason từ cancellation AFTER snapshot của chính event; Restore reason từ cancellation BEFORE snapshot của chính event; Distribute null. Không join current DocumentCancellation để gán lý do cho mọi chu kỳ.

Restore phải khớp PreviousStatus của snapshot event: InProgress **hoặc** Distributed. Nếu khôi phục Distributed thiếu readiness, thao tác write vẫn do service hiện hành từ chối; endpoint đọc không thay đổi quy tắc này.

`coverage`: `"V2LifecycleOnly"`. Metadata này nói rõ không trả audit trường/PDF/legacy trong đợt đầu. Không hiển thị “toàn bộ lịch sử công văn”. Nếu cần full history, phải mở rộng enum/event DTO và nguồn riêng trong contract tiếp theo, không merge theo timestamps rồi bỏ duplicate bằng phỏng đoán.

### Parser ChangesJson

- Xác minh JSON object, giới hạn kích thước theo dữ liệu writer hiện hành và DB giới hạn đã kiểm chứng; không deserialize thành dynamic rồi phát nguyên JSON ra client.
- Writer lifecycle có Status before/after và Cancellation snapshot. Parser phải bám đúng casing/schema writer (test payload thật của service); phân biệt audit update có field tên Status và schema lifecycle chuẩn. Audit edit không có Status → không phải lifecycle; Status present nhưng malformed → `HISTORY_DATA_INVALID`503, không âm thầm bỏ qua.
- Casing đã đối chiếu writer: root `Status`/`Cancellation`, bên trong `before`/`after`; Cancellation snapshot dùng `PreviousStatus`, `Reason`, `CancelledByUserId`, `CancelledAt`, `RestoredByUserId`, `RestoredAt`. Không normalize tên khóa tùy ý rồi bỏ qua field không đọc được.
- Kiểm transition Distribute InProgress→Distributed, Cancel InProgress/Distributed→Cancelled, Restore Cancelled→InProgress/Distributed; actors GUID khácrỗng, versiondương/timeUTC. Lý do Cancel/Restore từ đúng snapshot chu kỳ. Event lỗi không được hiển thị thành thành công.
- No-op write không tăng version/không append event thì timeline không tự tạo event.
- Dữ liệu legacy chỉ có DocumentStatusHistory/Note không được giả làm versioned event. Đợt1 không đọc nguồn đó; UI coverage rõ. Migrate/union legacy cần contract riêng với source/availability/type semantics, không đoán.

## Errors và frontend

400 query/ID format, 401 signedout, 403 không có capability ở partner hoặc actor inactive, 404 inaccessible/missing theo service, 503 authority/DB/unparseable audit. Frontend decoder malformed success →502. Không leak raw JSON, exception/SQL hay cancellation reason trong log lỗi.

Trước trả document history resolve lại read authority để giảm khoảng chờ thu hồi quyền; fresh scope thiếuID→404, authorityerror→503. Query DB xong rồi resolve, không giữ DB transaction qua network authority. Partner quyền từ signed token/current policy; không cachecanManage qua sessionepoch.

UI request key: service/entityId/throughVersion/page/size/sessionepoch. 401/403/404 xóa timeline cũ, ngăn có reason nằm trong DOM ẩn. 503 không gắn success và không fallback event gần nhất từdetail. Retry chỉ GET. Text wrap/horizontal scrolling trong card nếuID dài, datetime địa phương ghi rõ; timeline không phụ thuộc màu, keyboardfocus link/page controls chuẩnVIEN.

## Acceptance

| ID | Ca | Kết quả |
|---|---|---|
| J23-A01 | Partner Create→Update→Delete→Restore | 4event đúng actor/version/time/order; NotRecorded, không dựng diff |
| J23-A02 | Softdeleted partner deep link | Admin đọc timeline; reader403; missing404 |
| J23-A03 | Dùng actorName từ currentUser cho tất cả events | Regression phải bắt sai; chỉID khi chưa có directory xác minh |
| J23-A04 | Thêm audit mới giữa page1/2 | throughVersion cũ giữ không lặp/mất event; tải lại mới thấy event |
| J23-A05 | Version vượt JSsafe / watermark quá lớn/leadingzero | Chuỗi vẫn chính xác; invalid400 không làm tròn |
| J23-A06 | HủyA→restoreInProgress→distribute→hủyB→restoreDistributed | Mỗi event đúng lý do riêngA/B và status trước hủy |
| J23-A07 | `DocumentCancellation` chỉ chứa chu kỳB | Lịch sửA vẫn lấy từ event auditA, không bị ghi đè |
| J23-A08 | Legacy chỉ có Note/statushistory | Không dựng versioned lifecycle; coverageV2LifecycleOnly rõ |
| J23-A09 | Scope công văn bị thu hồi/ID ngoài phạm vi | 404; không trả reason/time/actor hoặc gợi ý công văn tồn tại |
| J23-A10 | Malformed JSON/missingreason/invalidstatus/time | 503/HISTORY_DATA_INVALID; khôngdropdòng và báo thành công |
| J23-A11 | RequestA chậm, mởdeepLinkB/token thay đổi | RequestA abort; không ghiA lênB/session mới |
| J23-A12 | Title/reasonXSS/4.000 ký tự/keyboard/mobileVIEN | Escape/wrap, focus đúng; không thiếu lý do vì cắt lưu dữ liệu |
| J23-A13 | No-op/retry lifecycle/auditunique | Không xuất event giả/lặp; writer invariants giữ nguyên |
| J23-A14 | GET qua gateway | Partner wildcard hiện có route được, historydocuments cần routeGET/OPTIONS cụ thể mới |

## File triển khai sau review

Partner controller/application/query DTO trong service hiện hành; không sửa business transaction writer nếu chỉ đọcmetadata. Frontend partner detail mới, services/type/dictionary, listdeepLink. Document history controller/query ở `workflows/business/document-service/Queries`, typed parser ở Query/Editing tương ứng, DTO trong Models/DTOs. Gateway thêm documenthistory routeGET/OPTIONS port5002. Tests parser+SQLite/protocol+SQL snapshot/capability/security+UI/browser; phân biệt từng tầng bằng chứng.
