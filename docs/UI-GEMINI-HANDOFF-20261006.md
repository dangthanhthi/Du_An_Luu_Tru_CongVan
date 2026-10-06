# DAS — Báo cáo UI và mô tả dự án để bàn giao cho Gemini

Ngày 06/10/2026. Báo cáo đối chiếu code, checkpoint và bản local tại thời điểm lập báo cáo, trước lượt sửa frontend song song sau khi bàn giao. Codex không thay đổi UI trong lượt lập báo cáo. Không coi trang thiếu dữ liệu/quyền là chưa viết giao diện. Các đề xuất mở rộng được ghi riêng; chưa mặc nhiên là yêu cầu mentor đã chốt.

**Lưu ý phiên bản:** Sau khi bàn giao, các file email-integration, document detail, My Staff, notifications, partners và footer đã có thay đổi frontend từ lượt làm việc khác. Backlog bên dưới là mốc đối chiếu trước các thay đổi đó; chưa kết luận chúng đã được sửa hoặc kiểm thử. Commit database của Codex không bao gồm những thay đổi frontend chưa review này.

## 1. Kết luận hiện tại

UI nghiệp vụ chính đã có: điều hướng DAS, đăng nhập/đăng xuất, danh sách ba loại công văn, đăng ký/sửa/chi tiết, trạng thái hủy/khôi phục/phân phối, PDF hiện hành, task công văn, đối tác, danh mục, tổ chức, báo cáo Excel, dashboard hồ sơ chưa hoàn tất, My Staff và chuông thông báo.

UI **chưa hoàn thiện để nghiệm thu sử dụng thật**. Có hai nhóm khác nhau:

- Việc frontend có thể làm ngay: ngôn ngữ, trình bày chi tiết, My Staff, thông báo lịch sử, phản hồi lỗi và rà soát trang email cũ.
- Việc chờ dữ liệu/quyền/tích hợp: công văn V2, dashboard/report/My Staff qua authority thật, scanner PDF, TMS và SMTP. Không được làm UI trông có vẻ hoạt động bằng cách bỏ kiểm quyền hoặc giả thành công.

G7 vẫn **Partial/prepared**; G8 chưa UAT/signoff. Không dùng số giai đoạn hoặc số bài test để suy ra tỷ lệ nghiệm thu.

## 2. Backlog UI cụ thể

| Ưu tiên | Mục | Điều đã xác nhận | Công việc/tiêu chí hoàn thành |
|---|---|---|---|
| P0 | Trang cấu hình email cũ | `apps/email-integration` vẫn có page. View lưu toàn bộ settings, gồm `appPassword`, vào `localStorage`; báo lưu thành công cục bộ. Hai API Next `/api/email/test` và `/api/email/scan` đang cố ý trả 503 `INTEGRATION_DEFERRED`. | Trong thời gian hoãn, đưa trang về trạng thái hoãn rõ ràng và không cho nhập/lưu secret. Không mở API/worker/OCR. Thiết kế việc chuyển config sang backend phải chờ contract/quyền được duyệt; không tự chạy cleanup dữ liệu trên máy người dùng trong lượt báo cáo. |
| P1 | Trang chi tiết công văn | Đã hiển thị header, nơi nhận, PDF/task và thao tác trạng thái. Nhiều trường trong `details` đã được nhập/lưu nhưng chưa được render: ngày nhận, phương thức, loại/phân loại, số hợp đồng, nơi nhận khác, thông tin khác. Originator/inputter/lastModifier là ID trong DTO, chưa có phần trình bày đầy đủ. | Render các trường sẵn có theo Incoming/Outgoing/Internal. Dùng tên snapshot/dữ liệu đã được cấp; không đoán tên từ GUID. Có trạng thái “chưa có dữ liệu”, đúng ngôn ngữ và nội dung dài. Audit timeline cần API được cấp riêng, chưa được coi là có sẵn. |
| P1 | My Staff | Có trang/table thành viên và table task; task hiển thị `assigneeUserId`, status thô và `dueAt` thô. Chưa có lựa chọn một nhân sự để xem task của người đó; không có tìm kiếm/lọc UI. Dùng chung một page cho hai tập dữ liệu. | Trước mắt hiển thị tên người thực hiện/người được giao khi có mapping được cấp, định dạng ngày VN, status dễ hiểu, empty/error/loading rõ. Đề xuất UX: chọn nhân sự, lọc task và làm rõ phân trang hai tập; cần giữ paging contract hiện tại hoặc thống nhất thay đổi với backend. Kiểm chứng trưởng phòng/tổng giám đốc xem cấp dưới khi authority/hierarchy thật được bàn giao. |
| P1 | Thông báo lịch sử | Chuông đã gọi API thật, số chưa đọc, đọc từng/all, link nội bộ và tải lại. Client chỉ lấy page 1, tối đa 20; popover không có tải thêm hoặc trang lịch sử đầy đủ. | Thêm lịch sử/phân trang nếu cần xem quá 20; xác nhận server contract trước đổi client. Không tuyên bố realtime SignalR đã nối: dropdown hiện polling mỗi 60 giây. |
| P1 | Ngôn ngữ VI/EN | Nhiều màn hình mới dùng chữ Việt trực tiếp: V2 form/detail/PDF/task, My Staff, report, overview, thông báo. Trang đối tác local hiển thị `0–0 of 0`, aria phân trang còn tiếng Anh. | Đưa label/error/status/date format qua dictionary dùng chung, kiểm cả `/vi` và `/en`. Không đổi mã nghiệp vụ lưu trong DB. |
| P1 | Lỗi/quyền/trạng thái chờ | Nhiều trang đã có alert/retry. My Staff và dashboard gom nhiều lỗi vào một câu “chờ kết nối”; chưa phân biệt rõ đăng nhập hết hạn, không có quyền, thiếu cấu hình và lỗi mạng. Form/detail có chỗ đưa thông báo API trực tiếp. | Chuẩn hóa 401/403/409/503/network ở presentation layer; giữ thông tin xung đột và “chưa rõ kết quả”, không tự resend mutation. Backend phải tiếp tục quyết định quyền. |
| P2 | Dashboard | Route DAS overview hiện chỉ render `IncompleteOverview`: tổng hồ sơ chưa hoàn tất và nhóm phòng. `StatCards`, `DocumentChart`, `DocumentByStatus`, `RecentDocuments` còn source nhưng không được route này dùng. | Dashboard tối thiểu đã có. Dashboard thống kê rộng hơn là đề xuất UX, cần dữ liệu/API đã được cấp và phạm vi thống nhất. Không ghép chart demo/template hoặc tính số toàn công ty từ một page dữ liệu. |
| P2 | Task công văn | Đã có người được giao, tiêu đề, lịch sử yêu cầu, PendingConfiguration/UnknownOutcome/retry/reconcile. Lịch sử tối đa 50 yêu cầu của người hiện tại. Chưa có thao tác mở task TMS hoặc form task đầy đủ hạn/ưu tiên/mô tả. | Polish bố cục/trạng thái/correlation. Link TMS, hạn/ưu tiên và task detail là mở rộng chờ API TMS; không suy đoán URL hay gửi thêm field chưa được chấp nhận. |
| P2 | Bộ lọc công văn/report | List có loại, phạm vi, tìm kiếm, trạng thái và phân trang; report có loại, phòng, includeRecent, phân trang/XLSX. Chưa có bộ lọc UI rộng theo công ty/khoảng ngày/người gửi trên list hiện hành. | Đề xuất mở rộng sau khi đối chiếu query DTO/backend. Không thêm control không tác dụng hoặc lọc cục bộ chỉ trên một page rồi gọi là tìm toàn bộ. |
| P2 | Branding/template | Menu đang chạy dùng `buildDasNavigation`, không dùng menu demo thương mại/academy. Nhiều route template đã bị vô hiệu hóa. Footer bản local vẫn “Made with … PIXINVENT”. | Rà branding/footer và liên kết không thuộc DAS. Kiểm license/attribution trước bỏ credit. Không xóa hàng loạt template source vì có dependency dùng chung. |
| Kiểm chứng | Responsive/accessibility | Nhiều view đã có Grid responsive, table overflow, loading labels; My Staff còn bảng/Stack đơn giản. Chưa có nghiệm thu đầy đủ trên mobile, keyboard, screen reader và PDF/XLSX luồng thật. | Kiểm desktop/mobile, tab/focus/dialog, touch targets, nội dung dài, bảng rộng, loading/empty/error/403/503/conflict. Ghi là chưa kiểm nếu chưa có bằng chứng; không tự kết luận toàn bộ UI lỗi responsive. |

## 3. Những phần bị chặn bởi tích hợp, không phải chưa viết UI

1. `document-service/Program.cs` hiện đăng ký `UnavailableDocumentV2Authority`, `UnavailableReportAuthority`, `UnavailableStaffAuthority`, `UnavailableTmsConnector` và directory/audience chưa nối. API V2 có thể trả 503 theo thiết kế. Không cấp quyền bằng cách sửa `allowedActions`, role, capability cache hoặc dữ liệu browser.
2. Tài khoản Development có role nhưng JWT chưa có các capability như `CatalogManage`, `DocumentRegisterIncoming`, `DocumentRegisterDepartment`. Nút tạo/sửa/danh mục có thể không hiện; điều đó chưa chứng minh UI thiếu nút. Cần authority/capability hợp lệ, không thêm blanket Admin bypass.
3. Trang đối tác đang rỗng vì store local chưa có dữ liệu khách hàng; CRUD/audit/restore code đã có. Không nạp khách hàng giả rồi ghi rằng migration khách hàng hoàn tất.
4. PDF có UI upload/replace/view/retry. Readiness scanner/quyền/storage thật chưa bàn giao. Không đánh dấu mọi upload là Clean/Ready để demo luồng thành công.
5. My Staff có UI và service DAS. Dữ liệu cấp dưới/hierarchy/ID cùng task từ TMS chờ nguồn thật. My Staff không tự cấp quyền xem công văn/PDF của mọi nhân viên.
6. SMTP chưa có config và audience được duyệt. Lịch nhắc đã có logic; không gửi thư thật trong lượt làm UI.
7. EAP do nhóm khác phụ trách đến khi chủ dự án mở lại; OCR vẫn hoãn. Fax chưa nghiệm thu/kết nối thật. Không biến phần hoãn thành backlog triển khai ngay của Gemini.

## 4. Mô tả dự án hiện tại

**DAS — Document Administration System** là hệ thống quản lý công văn đến, đi và nội bộ. Nó quản lý đăng ký/số công văn, thông tin đơn vị/người gửi/nơi nhận, trạng thái xử lý, PDF hiện hành, quan hệ giữa công văn, báo cáo hồ sơ chưa hoàn tất, nhắc hạn và yêu cầu task liên quan. TMS là hệ thống của nhóm khác; DAS tạo/lưu intent và đối soát kết quả. EAP là nguồn identity/tổ chức do nhóm khác làm.

### Kiến trúc và vị trí source

Repository: `https://github.com/dangthanhthi/Du_An_Luu_Tru_CongVan`. Main đã xuất bản đến `9bddd340c7271667a0c73c2b7f82880a45fd7508` ở đầu lượt DB hiện tại; runtime phiên nhiều tab nằm ở `87ddc9c`. Nhánh đang làm database: `codex/database-readiness-20261006`; thay đổi mới chưa được coi đã có trên main cho đến khi có publication checkpoint.

Thư mục chính trên máy: `C:/Users/MSIIIIII/Desktop/Dự án taskmanager/DAS-Collaboration`.

- `frontend/`: Next.js 16.3.8, React 19.2.3, TypeScript, MUI 7, Vuexy; Node chuẩn kiểm chứng 22.23.3. VI/EN; API facades tại `src/services/das`, contracts tại `src/types/das`, view nghiệp vụ tại `src/views/apps`.
- `backend/`: .NET 10, ASP.NET Core, EF Core; Gateway Ocelot và 6 service auth/document/partner/files/notification/email-worker. Lock packages theo từng project; Email Worker EF9, Notification EF10.0.0, các store còn lại EF10.0.3. Không nâng major framework/package để “làm đẹp UI”.
- `database/migrations/<service>/`: lịch sử migration/snapshot của từng store, compile vào đúng service bằng `backend/Directory.Build.props`. Sáu store riêng, không chung DbContext. Prisma frontend chỉ là template auth, không phải database công văn chính.
- `workflows/business/`: nghiệp vụ tách theo service, compile-linked; không tạo bản code thứ hai ở folder cũ.
- `docs/`: contracts/runbooks/plan/checkpoint/evidence; `tests/`: QA/layout; backend/frontend có tests riêng; `tools/`: entrypoint kiểm chứng. `.artifacts/` là runtime/log/cache/QA riêng không publish.
- Source cũ đã được lưu ở `../archive/legacy-sources-20261006`. Không làm việc/push từ repo cha hoặc cây archive.

### Nghiệp vụ đã chốt, phải giữ nguyên

- Ba bộ đếm riêng theo loại công văn/năm, dùng chung giữa company/phòng. Hiển thị `XXXX` bốn chữ số đến 9999; chỉ từ 10000 mới thành năm chữ số. Không đặt placeholder mặc định `XXXXX`.
- Registration Date lấy ngày hiện tại lúc đăng ký; không hồi tố. Số công văn dựa trên ngày đăng ký. Issued Date có thể là ngày quá khứ. Dự kiến bắt đầu sử dụng năm 2027.
- Cho sửa company và phòng sau đăng ký nhưng giữ nguyên số thứ tự/năm/ngày đăng ký. Không tự cấp số mới khi sửa.
- Originator là người thực sự gửi đi, khác người nhập hộ. Department mặc định là phòng chính, có thể chọn các phòng được cấp cho người đó. Group và department dùng cấu trúc tổ chức phân cấp, không suy ra mọi group là department.
- MGT là cấp cao nhất, parent null; sơ đồ mentor: MGT → ADM/DRI/HSE; ADM → IT/HR (group). ID/mapping external chưa chốt. Không sửa migration seed hoặc suy đoán mapping MGM/MGT chỉ từ tên viết tắt.
- Cancelled khôi phục về trạng thái trước khi hủy; không luôn về InProgress.
- Nhắc hồ sơ đi/nội bộ chưa hoàn tất khi **quá 7 ngày** theo lịch Việt Nam; đúng 7 ngày chưa nhắc. Thứ Hai 08:00 Việt Nam. Incoming/Cancelled bị loại. Không dùng lại mốc 14 ngày.
- My Staff nằm trong DAS. Cấp quản lý xem thành viên/task của cấp dưới trong phạm vi được cấp, kể cả nhiều cấp; quyền công văn/PDF được kiểm riêng.
- Admin chỉ quản trị danh mục; role Admin không tự cho quyền xem/sửa toàn bộ công văn.
- Backend quyết định quyền, số đăng ký, completeness, version và idempotency. Frontend phải giữ session ownership, response/stream checks và retry cùng yêu cầu khi kết quả chưa rõ.

### Chức năng đã triển khai và các gate

G0/G1: baseline/contracts/master data. G2: policy/capability, credential login adapter, phối hợp session nhiều tab. G3: đăng ký/sửa/lifecycle/relations/PDF. G4: durable notification/dedup/retry/history. G5: incomplete report/XLSX/reminder. G6: My Staff/task intent/reconcile. G7: preflight, functional CI, SQL/restore/load/runbooks — **Partial/prepared**. G8: kịch bản UAT đã chuẩn bị, chưa ký duyệt/pilot.

Chặng session đã có frontend291/291, typecheck/lint/full build, Python93/93, layout10/10; transport browser fixture giả lập. CI hosted trước đó thuộc runtime `abb54a9`: backend629 mỗi OS, frontend223, SQL77 qua; full workflow vẫn fail do audit6high. Các số này có scope/thời điểm riêng, không là nghiệm thu bản UI mới.

Chặng database đã bổ sung baseline/snapshot Email Worker, sửa startup SQL và bộ schema kiểm chứng: backend không SQL631/631, Python QA94/94, layout13/13 trên cây Git riêng, offline schema6/6, SQL riêng Email Worker4/4 và Notification5/5 qua. SQL core đầy đủ còn bài allocation-lock timeout tại DocumentService45/46; chưa có dữ liệu/mapping khách hàng. Xem [checkpoint database](DATABASE-CHECKPOINT-20261006.md) và [evidence](DATABASE-VERIFICATION-20261006.json). Các số này không kiểm chứng frontend mới hoặc tích hợp/môi trường khách hàng.

Thiếu đầu vào thật: customer export/PDF/mapping, authority/session/CSRF/revocation, TMS contract/sandbox/IDs/hierarchy, SMTP/audience, scanner/storage/deployment/RPO/RTO/SLA, Fax, license/security/audit/registry/signing/scanning và mentor UAT/signoff.

### Local preview

Web `http://127.0.0.1:3211`; Gateway `8080`; auth `5001`, document `5002`, partner `5003`, files `5004`, notification `5005`, email-worker `5007`. Development dùng SQLite riêng, không phải DB khách hàng. Worker/SMTP/OCR/tích hợp thật đang tắt. Health 200 không tự chứng minh API V2/authority hoạt động.

Test login Development: `secretary_user`, `admin_user`, `employee_user`, cùng password demo `password`. Không dùng tài khoản demo trong production. Không đưa JWT/config thật, `.env`, DB/PDF, backup, log hoặc thông tin mentor vào prompt/Git.

## 5. File nên đưa cho Gemini

Đọc `docs/COLLABORATION-STATUS.md`, `docs/CLIENT-SESSION-CHECKPOINT-20261006.md`, báo cáo này và `docs/GEMINI-UI-PROMPT-20261006.md` trước. Sau đó chỉ đưa source của module cần sửa, DTO/API facade và test liên quan.

Nếu dùng Gemini qua MCP advisory: MCP **không tự đọc file/path/repository, không mở web hoặc sửa code**. Phải gửi nội dung/excerpt/diff cụ thể trong context; path chỉ là nhãn. Nếu dùng Gemini trong IDE có filesystem, cho nó đọc đúng thư mục canonical. Không gửi toàn bộ repo/cache/secret chỉ để nhờ chỉnh UI.

Gemini MCP hiện báo `server_version=4-30min`, nhưng lượt advisory database ngày này thất bại: hai tài khoản cùng 503. Không khẳng định Gemini đã review báo cáo hoặc code hiện tại.
