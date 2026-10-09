# Checkpoint hoàn thiện J20–J23 local — 09/10/2026

Trạng thái: **IMPLEMENTED_LOCAL / BROWSER_VERIFIED**. Đã thực hiện các Task 1–7 của kế hoạch J21–J23, backend/gateway/frontend và kiểm chứng local. Các kết quả này không thay thế nghiệm thu TMS, authority/capability production, dữ liệu khách hàng hoặc UAT. G7 vẫn Partial/prepared; G8 chưa có customer signoff.

## Nguồn và phạm vi

- Nguồn phát triển: `DAS-Collaboration`, nhánh local `codex/j21-j23-20261008`, HEAD `db65153e23243f45defe290bf83281f6dfebc3b6`. Thay đổi lượt này nằm trong working tree. Không commit/push, không sửa checkout `DAS-Official-BE`, không reset hoặc ghi đè WIP.
- Quyền thực hiện: người dùng yêu cầu tiếp tục mọi phần có thể làm, tự chọn phương án và báo lại. Những lựa chọn kỹ thuật bên dưới được ghi để người dùng/đồng nghiệp review; không tự đặt quy định nghiệp vụ mới.
- EAP thuộc nhóm khác, OCR hoãn; Gemini MCP không sử dụng. Reminder vẫn quá 7 ngày, thứ Hai 08:00 Việt Nam; số công văn mặc định 4 chữ số, chỉ tăng 5 chữ số khi vượt 9999; khôi phục về trạng thái trước hủy.

## Đã thực hiện

| Phần | Kết quả | Giới hạn còn lại |
|---|---|---|
| J20 | Browser kiểm admin/reader, desktop/mobile/tablet, VI/EN, bàn phím, chuỗi dài và PDF thật đã lưu trong database QA. Sửa toolbar tràn ở 320px; menu ngang trên mobile dùng nút có tên, hỗ trợ Enter/Space, trap Tab, Escape và trả focus. | Không tuyên bố screen-reader certification hoặc UAT production. |
| J21 | GET tasks riêng, lọc assignee trên toàn bộ scope trước count/paging, phân trang nhân sự/công việc độc lập, kiểm lại authority; UI giữ filter qua staff page và xử lý stale/session/error. | TMS thật chưa có contract/sandbox. Browser xác minh staff từ DB và task unavailable/unknown; task positive dùng adapter fixture trong tests. |
| J22 | GET options capability từ server; admin list riêng lọc activity/search/group trước phân trang; fixed groups chỉ đọc. Ngừng/kích hoạt lại cùng ID/code bằng PUT version; lookup công văn vẫn active-only. UI owner/abort/clamp, thu hồi quyền và khóa mutation khi kết quả chưa chắc chắn. | Capability/authority của backend production phải do nhóm phụ trách cấu hình; không dùng quyền QA làm bằng chứng production. |
| J23 đối tác | GET audit có CatalogManage, hỗ trợ bản ghi soft-deleted, watermark phiên bản chuỗi Int64; metadata actor/time/action đã lưu, deep link và timeline VI/EN. | DB chưa ghi before/after của từng trường, nên không có field diff. Không dựng dữ liệu lịch sử từ bản ghi hiện tại. |
| J23 công văn | GET lifecycle history theo read scope, parser allowlist audit thực của writer; mỗi chu kỳ có đúng reason A/B, Restore về InProgress hoặc Distributed, bỏ metadata trước count/page. Watermark/UTCZ/Int64 string và fail-closed với audit không hợp lệ. | Coverage V2LifecycleOnly: chưa gồm edit trường, PDF hoặc toàn bộ lịch sử legacy. Legacy không xác minh được phải báo unavailable. |

UI lịch sử có thể đọc độc lập khi decoder summary legacy gặp lỗi 502 do phiên bản Int64 quá lớn; không dựng summary giả hoặc mở mutation. 401/403/404 vẫn chặn lịch sử theo quyền và existence. Panel xóa DOM cũ khi đổi ID/session/query hoặc gặp lỗi; không tự retry mutation.

Thông báo kết quả lưu đối tác chưa xác định đã tách create/update: tạo mới yêu cầu kiểm danh sách trước tạo lại; sửa yêu cầu tải phiên bản hiện tại. Không thay đổi guard/reload hay cách xử lý unknown outcome.

## Những quyết định đã tự chọn

1. Partner dùng policy CatalogManage hiện có; công văn dùng read authority hiện có. Không mở quyền chung cho Admin hoặc suy quản lý tổ chức từ role.
2. Phiên bản wire là chuỗi số thập phân canonical; frontend so sánh bằng BigInt. Page phải khớp đúng cardinality từ total/offset, không chỉ kiểm tối đa pageSize.
3. Partner writer hiện hành ghi UTC; SQL datetime2/SQLite làm mất DateTimeKind, query phục hồi UTC theo provenance đã kiểm. Không gán múi giờ cho legacy chưa xác minh. Wire UTC `Z`, UI ghi rõ Việt Nam UTC+7.
4. Snapshot DB nằm trong EF execution strategy, không giữ transaction qua network authority. RetryLimitExceededException trả 503 đã lọc thông tin, không rơi về 500 thô.
5. Audit metadata hợp lệ có thể lớn hơn 64KiB (200 người nhận Unicode). Parser general JSON tối đa 1MiB, lifecycle tối đa 64KiB, depth 8; không bỏ audit lỗi rồi trả thành công.
6. Local preview dùng QA host có authority/scanner fixture sẵn có, chỉ account test persisted active được cấp quyền fixture. Không cấp quyền tài khoản thật; không giả TMS task. SMTP/intake/reminder thật tắt, không gửi email.
7. QA host phải khởi động với working directory chứa MvcTestingAppManifest; lỗi manifest startup đã sửa ở launcher private. Không thay product để né lỗi hạ tầng kiểm thử.
8. Menu resize mobile → desktop → mobile đã kiểm; drawer đóng khi về mobile, toolbar không tràn. Không thêm quy tắc mở quyền/bố cục chưa có yêu cầu.

## Kiểm chứng

Evidence private: `.artifacts/qa/j21-j23-implementation-20261008-01/`. Không đưa DB/PDF/secret/ảnh QA lên GitHub chính.

| Tầng | Kết quả | Evidence |
|---|---|---|
| Frontend toàn bộ | 479/479, concurrency 1, heap 2048MiB | `frontend-completion-closure.log` |
| TypeScript | Exit 0 | `typecheck-completion-closure.log` |
| ESLint | 0 errors, 66 warnings cũ | `lint-completion-closure.log` |
| Frontend production build | Exit 0 | `web-completion-build-closure.log` |
| Document non-SQL | 544/544, không failed/skipped | `document-final.log` |
| Partner non-SQL | 62/62, không failed/skipped | `partner-final.log` |
| Native SQL Server | J22 admin 3/3, document history 1/1, partner audit 1/1 | `catalog-sql-final.log`, `document-sql-final.log`, `partner-sql-final.log` |
| Gateway config | 4/4 | `gateway-config-final.log` |
| Gateway HTTP + persisted QA DB | 77 assertions đạt | `live-acceptance.json` |
| Backend Release solution | Exit 0, 1 warning xUnit2031 đã tồn tại | `build-final.log` |
| QA host Release | Exit 0, 0 warning/error | `runtime-build-final.log` |
| Python QA | 100/100 | `.artifacts/qa/j21-j23-completion-20261009-01/python-qa-closure.log` |
| Source/layout | Manifest hash cập nhật, diff whitespace đạt | `docs/source-layout-manifest.json` |
| Review độc lập | Spec/code quality PASS sau sửa các P2; không có blocker còn lại | `task-4-7-final-review.md` |

Backend và live HTTP kiểm trước các chỉnh presentation cuối; backend/API không thay sau đó. Frontend closure kiểm sau toolbar/menu/copy cuối. SQL tests chỉ tạo/xóa DB fixture có GUID riêng, không nhập/sửa DB khách hàng.

Browser: catalog ngừng/kích hoạt lại bằng Enter; reader không có nút thêm/sửa; reader mở partner history bị từ chối và không có event cũ. My Staff hiển thị 2 người từ Auth DB QA; chọn assignee bằng bàn phím giữ chế độ TMS unavailable. Timeline đối tác 4 events; công văn 5 lifecycle events, lý do dài 3800 ký tự và `<script>` là literal text. PDF thật 272163 bytes từ Downloads đã ở local DB, trang 1/2 và 2/2 render canvas. Tên danh mục 200 ký tự liên tục đã lưu qua UI và kiểm tại320px: cell xuống dòng, page305=viewport305. Sau đó khôi phục tên mẫu qua UI, cùng ID/code, active và version7 được xác minh trực tiếp DB. Các trạng thái loading đã chờ xong trước kết luận. Browser evidence tổng hợp ở `browser-evidence-final.json`; evidence lệnh và hash source ở `completion-evidence.json`.

Fresh npm audit: runtime `--omit=dev` không có advisory; audit toàn bộ có 6 high dẫn về `braces` và công cụ glob/lint. [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) chưa có patched version tại thời điểm kiểm. Không chạy `audit fix --force` vì đề xuất downgrade major Next lint/stylelint/globby không phải bản vá tương thích. Đây vẫn là security gate; runtime audit 0 không chứng minh toàn bộ sản phẩm an toàn. Log JSON private ghi nguyên kết quả.

## Môi trường để tiếp tục

- Local preview: `http://127.0.0.1:3211`, gateway `8080`, Auth `5001`, Document/Partner/Files `5002/5003/5004` qua QA host, Notification `5005`, Email host `5007` với workers tắt.
- Runtime/launchers/process records private: `.artifacts/local-preview/20261007-j20/`. PID có thể đổi; đối chiếu command/port/owner hiện tại trước dừng/restart. Không kill PID lịch sử hoặc tiến trình của người dùng.
- Native SQL Server đã chạy trước lượt này, giữ nguyên. Không dựng thêm Docker/SQL server tiêu tốn RAM. Nguồn không chứa credential QA/local, real PDF hoặc DB.

## Còn cần điều kiện bên ngoài

1. TMS contract/sandbox: filter-before-count/paging, thứ tự ổn định, ID mapping, lifecycle task/reconciliation và scope tổ chức thật.
2. Auth/authority/capability production của nhóm phụ trách; scope đọc công văn/nhân sự, EAP mapping khi nhóm đó cung cấp. EAP vẫn hoãn theo yêu cầu người dùng.
3. SMTP/IMAP, audience/directory và scanner thật; hợp đồng fax thật nếu triển khai. OCR vẫn hoãn.
4. Customer DB/PDF/export, legacy audit provenance/mapping, chiến lược migration/RPO/RTO/SLA và rehearsals trên môi trường đích. Không lấy fixture làm dữ liệu khách hàng.
5. Môi trường deploy/registry/signing/scanning, license commercial/assets, khắc phục hoặc review advisory chưa có bản vá, UAT/pilot và ký duyệt mentor.
6. Ghép vào backend chính cùng nhóm theo integration plan và yêu cầu Git mới. Không tự push các WIP/code/test/checkpoint lên bốn nhánh chính thức.

Lượt này hoàn tất phần nội bộ J20–J23 có đủ đầu vào. Chỉ mở các gate còn lại khi có điều kiện tương ứng; không sửa quy định mentor hoặc thay hệ thống bên ngoài bằng mock production.


> Trạng thái môi trường được cập nhật ở [checkpoint chốt local](LOCAL-FINISHING-CHECKPOINT-20261009.md): preview hiện dừng; không dùng PID/ports chạy trong đoạn lịch sử bên trên để kết luận đang online.
