# Tiến độ và phạm vi bản cộng tác

> Kiểm owner Gemini07/10: Codex xác nhận **8/8 owner,12/12 acceptance,19/19 audit+directory,31/31 canonical,368/368 frontend**, typecheck/lint qua; thêm 13/13 owner extended (gồm 8 ca gốc). Fixture chỉ đổi scheduling, assertions giữ nguyên; 1319 inputs ổn định. Chấp nhận nhánh clamp/J08–J09 trong phạm vi component. J20/browser và J21–J24 giữ gate; hai hash trước sửa trong tài liệu Gemini cần đính chính. [Biên bản](GEMINI-PARTNER-OWNER-VERIFICATION-20261007.md), [evidence](GEMINI-PARTNER-OWNER-VERIFICATION-EVIDENCE-20261007.json).

> Kiểm supplemental Gemini07/10: fresh **12/12 acceptance,19/19 probe,26/26 canonical,363/363 frontend**, typecheck/lint qua;1319 inputs ổn định và hash khớp. Email/lifecycle cùng ma trận J đã sửa. **Nhánh corrected GET đối tác còn5 owner counterexamples (T01–T03) trong8 probe:3 đối chứng qua**; J08/J09 chưa nghiệm thu hết. Không sửa runtime; [review](GEMINI-SUPPLEMENTAL-REVIEW-20261007.md), [evidence](GEMINI-SUPPLEMENTAL-REVIEW-EVIDENCE-20261007.json), [prompt](GEMINI-PARTNER-REQUEST-OWNER-FOLLOWUP-20261007.md). Browser/build mới, EAP/OCR/G7/G8 giữ gate.

> Kiểm bản khắc phục Gemini07/10: Codex chạy lại **19/19 probe,19/19 canonical mới,356/356 frontend**, typecheck/lint qua (66warnings),1319 inputs ổn định và12 hash khớp. **12 acceptance bổ sung có3pass/9fail thuộc6 nhóm S01–S06**; còn gate email, storage read-denied, lifecycle reconcile/restore copy và partner stale-cache race. Ma trận J03/J05/J18/J19 vẫn sai. Không sửa runtime trong lượt review này; [review](GEMINI-CORRECTION-REVIEW-20261007.md), [evidence](GEMINI-CORRECTION-REVIEW-EVIDENCE-20261007.json), [prompt sửa tiếp](GEMINI-UI-CORRECTION-FOLLOWUP-20261007.md). Giữ browser/build mới pending và EAP/OCR/G7/G8 gate.

> Rà báo cáo Gemini07/10: 19 probe mới có3 đối chứng qua và16 tiêu chí mong muốn thất bại trong8 nhóm R01–R08; thêm gap locale/a11y và sai ma trận J. Không sửa runtime trong lượt này;1.318 inputs vẫn khớp lượt642/337 trước đó. Các suite cũ qua không phủ những ca mới. Đã viết [review chi tiết](GEMINI-REPORT-VERIFICATION-20261007.md), [evidence](GEMINI-REPORT-VERIFICATION-EVIDENCE-20261007.json) và [prompt sửa](GEMINI-UI-CORRECTION-PROMPT-20261007.md). Browser/build mới chưa kiểm; EAP/OCR và G7/G8 giữ gate.

> Chặng kiểm luồng DAS07/10: **backend642/642**, **frontend337/337**, typecheck/lint qua (66 cảnh báo template), source ổn định. Thêm kiểm HTTP My Staff/danh mục/inbox và callback form/PDF/report/catalog/Staff; sửa gửi form lặp trước render và hiển thị nhãn danh mục lịch sử. Kiểm bằng dữ liệu giả lập; chưa browser→gateway/upstream thật, không chạy SQL Server lại hoặc full build, chưa commit/push. [Checkpoint](DAS-FLOW-TEST-CHECKPOINT-20261007.md), [evidence](DAS-FLOW-TEST-EVIDENCE-20261007.json). EAP/OCR tiếp tục hoãn; G7/G8 giữ gate.

> Xác nhận của người dùng: **chưa đủ thông tin dữ liệu khách hàng và chưa kiểm chứng trên môi trường thật**. G7 giữ trạng thái **Partial/prepared**, chưa nghiệm thu; G8 chưa UAT/signoff. Ước lượng 70–80% chỉ nói về phần chuẩn bị kỹ thuật, không phải tỷ lệ hoàn thành toàn bộ G7. Chưa có trọng số được duyệt để báo một tỷ lệ nghiệm thu đáng tin cậy.

> Chặng database mới nhất: SQL **87/87** gồm core84, load và hai restore cut độc lập (năm store/PDF và Email); source integrity/cleanup qua. Offline schema **6/6, 24 migrations**, Email không SQL13/13, Python QA96/96, layout13/13 trên HEAD archive cộng thay đổi database. Đã sửa singleton Email IDENTITY và guard giữ CHECK/default tùy chỉnh; chưa nhập dữ liệu khách hàng hoặc nghiệm thu Production. [Checkpoint](DATABASE-COMPLETION-CHECKPOINT-20261006.md), [evidence](DATABASE-COMPLETION-VERIFICATION-20261006.json). Chặng này chưa commit/push, frontend/Gemini đang sửa riêng không được kiểm ở đây; G7/G8 giữ gate.

> Chặng mới phối hợp phiên nhiều tab đã kiểm cục bộ: **frontend291/291**, typecheck/lint/full build, Python93/93, layout10/10;1.169 inputs và inventory khớp. Browser-v8 hai tab native/React đã kiểm với transport giả lập, đóng fixture. [Checkpoint hiện hành](CLIENT-SESSION-CHECKPOINT-20261006.md), [evidence](CLIENT-SESSION-VERIFICATION-20261006.json). Runtime đã push main `87ddc9c`, nguồn local/remote khớp; [cấu trúc local](LOCAL-WORKSPACE.md) và [publication evidence](CLIENT-SESSION-PUBLICATION-20261006.json). G2 tích hợp thật/G7/G8 vẫn giữ gate. Checkpoint tạm dừng bên dưới là lịch sử.

> Chặng trước: sửa rollback khi lưu refresh thất bại và logout menu xóa nhầm phiên mới. Local Windows **frontend231/231**, typecheck/lint/full build, Python93/93 và layout10/10 qua; nguồn đối chiếu hash. [Checkpoint phiên](SESSION-STORAGE-CHECKPOINT-20261006.md), [evidence](SESSION-STORAGE-VERIFICATION-20261006.json). Cross-tab atomicity/browser/authority thật vẫn chưa hoàn tất; CI dưới đây thuộc runtime trước patch này.

> Đã publish runtime `abb54a9` lên main và kiểm CI hosted thật: **backend629 mỗi OS, frontend223, SQL77, layout/QA qua**. Full workflow vẫn failure do audit6high. [Checkpoint GitHub/CI](HOSTED-CI-CHECKPOINT-20261006.md), [evidence](HOSTED-CI-VERIFICATION-20261006.json). G7/G8 giữ các gate còn mở.

> Chặng SQL trước patch phiên: **75 core SQL + load + restore** qua, source integrity/cleanup qua; bundle 5 backup + PDF xác minh checksum. Python QA **93/93**, layout **10/10**. [Checkpoint SQL](SQL-CHECKPOINT-20261006.md), [evidence](SQL-VERIFICATION-20261006.json). SQL hosted CI đã qua trên runtime abb54a9 theo checkpoint GitHub/CI; G7/G8 vẫn giữ gate thực tế.

## Phân công EAP — cập nhật ngày 06/10/2026

Theo yêu cầu người dùng, **EAP do người khác phụ trách; Codex không triển khai EAP cho đến khi người dùng yêu cầu rõ ràng mở lại phần này**. Yêu cầu chung “tiếp tục dự án” chỉ tiếp tục phần DAS, không mở lại EAP.

Phạm vi EAP được loại khỏi công việc hiện tại của Codex gồm kết nối SSO/Identity EAP, API tổ chức EAP, đồng bộ người dùng EAP qua RabbitMQ và thiết lập NetBird phục vụ EAP. My Staff và nghiệp vụ task trong DAS vẫn thuộc phạm vi DAS; dữ liệu/quyền/mapping phụ thuộc EAP chờ người phụ trách bàn giao, không tự giả định đã có tích hợp thật.

Tài liệu và cấu hình mentor mới cung cấp mang tên Task Management; chưa xác nhận client/credential được cấp cho DAS. Không sử dụng credential đó hoặc đưa file `.env` vào Git/frontend. OCR vẫn hoãn theo yêu cầu trước.

Bản cộng tác ngày 06/10/2026. G7 **Partial/prepared**; G8 chưa UAT/signoff. Không dùng số giai đoạn hoặc số lượt test để suy tỷ lệ hoàn thành dự án. Ước lượng 70% trước đây không có mẫu số được duyệt; theo dõi bằng phần đã kiểm và gate còn mở.

## Nguồn trước khi sắp xếp

Linux Node 22: 214/214 frontend tests, typecheck/lint/full build/source integrity qua. Python QA 57/57. Image source tương ứng: 25 HTTP checks, 77 template paths và 6 kiểm tra DOM/title/menu ẩn danh qua. Backend 608 non-SQL và các SQL/restore/startup/load checks có bằng chứng ở nguồn phát triển trước đó, theo scope giả lập riêng. Những số này **chưa tự chứng minh layout mới**.

Kiểm tra sau sắp xếp đã qua: **608/608 backend non-SQL tests và Gateway Release build; 214/214 frontend tests, typecheck, lint (0 lỗi/66 warnings), full build; 10/10 layout và 57/57 Python QA tests**. Linux Node22/.NET10, nguồn cố định, source integrity và cleanup qua. 1,532 file đầu vào build đối chiếu byte với cây Git đều khớp. Backend inputs không đổi so với lượt backend đã qua; các chỉnh sửa tiếp theo chỉ ở build Prisma frontend/công cụ kiểm. Xem [bằng chứng](PUBLICATION-VERIFICATION.json). Đây là kiểm tra cục bộ và Git source archive; hosted clean-clone CI, Windows frontend, live gateway/authority/customer migration/UAT chưa được chấp nhận.

## Phần đã triển khai nội bộ

Cập nhật sau xác nhận **quá 7 ngày**: backend Linux **629/629 non-SQL**, Gateway Release build; frontend Linux **223/223**, typecheck/lint/full build; Python QA hiện hành **79/79** và layout **10/10** đã qua. Nguồn runtime đầu vào đã đối chiếu hash với cây hiện hành; source integrity/cleanup qua. Đã sửa login ownership, preflight PDF/CLI, Email Worker startup gates và quyền Admin legacy. CLI riêng **26/26 trên Windows và Linux**. Xem [checkpoint](COMPLETION-CHECKPOINT-20261006.md), [checkpoint CLI](PREFLIGHT-CHECKPOINT-20261006.md) và [bằng chứng mới](COMPLETION-VERIFICATION-20261006.json). Các gate thật dưới đây vẫn mở; những số này không thay UAT hoặc chấp thuận bảo mật/license.

| Giai đoạn | Phần đã có | Còn chờ |
|---|---|---|
| G0/G1 | Baseline, contracts, master data/company/phòng/đối tác, CRUD/audit/restore | Mapping và nguồn chuẩn EAP/legacy |
| G2 | Backend policies/capability/fail-closed, credential login adapter, single-record/Web Locks phối hợp nhiều tab, inherited mutation owner và cleanup/recovery đã kiểm native browser giả lập | browser→gateway→authority/session/CSRF/revocation thật |
| G3 | 3 loại công văn/counters, register/edit/distribute/cancel/restore/relations/PDF/idempotency/audit | File/scanner/live authority và UAT |
| G4 | Durable notification inbox/outbox/dedup/retry/history | SMTP thật; EAP/OCR hoãn; Fax sau |
| G5 | Dashboard/report/XLSX cùng scope, reminder7ngày/thứHai08VN | Audience/directory/SMTP config thật |
| G6 | DAS My Staff/tasks, task intent/retry/reconcile/history | TMS contract/sandbox/hierarchy/ID mapping và authority thật |
| G7 | Preflight đã siết schema/PDF, hosted functional CI, image/runtime/config, Production DB checks, SQL/restore 5 DB+PDF, load/idempotency, runbooks | customer export/mapping/RPO/RTO/SLA; security/full-audit/license; registry/signing/scanning |
| G8 | 18 kịch bản UAT đã chuẩn bị | Môi trường/dữ liệu thật, mentor chạy và ký duyệt/pilot |

## Backlog cần đọc trước khi sửa

Frontend Windows Node22.23.3 đã qua **223/223 tests**, typecheck/lint/full build, nguồn giữ nguyên và đối chiếu với runtime inputs hiện hành khớp. Xem [checkpoint Windows](WINDOWS-FRONTEND-CHECKPOINT-20261006.md). Hosted clean-clone CI và browser/gateway/authority/UAT vẫn là gate riêng.

1. G2: Phối hợp phiên nhiều tab, response/body/stream ownership, inherited mutation owner và cleanup/recovery đã kiểm cục bộ; frontend291/291, native browser/React transport giả lập. Runtime87ddc9c đã publish. BFF/session/authority thật và nghiệm thu browser/gateway còn mở. Xem [checkpoint](CLIENT-SESSION-CHECKPOINT-20261006.md).
   Đã bỏ bypass Admin ở API công văn legacy; Admin chỉ có quyền quản trị master data, quyền công văn theo vai trò/phạm vi nghiệp vụ. Các bài test cũ dùng Admin để tạo công văn đã chuyển sang vai trò thư ký phù hợp. Kiểm tra HTTP xác nhận list/detail/file/mutation không tiết lộ hoặc thay đổi công văn cho Admin không có quyền nghiệp vụ.
2. G7: Đã sửa top-level nonobject, bool/year, PDF metadata/path/link/signature/hash/size; sau đó bổ sung CLI chống overwrite/hardlink/traversal/ADS và parse/hash cùng snapshot. 26/26 focused tests Windows và Linux qua, giữ read-only semantics. Đây chỉ là preflight, chưa nhập dữ liệu khách hàng hay thay thế scanner. Xem [hướng dẫn](MIGRATION-PREFLIGHT.md).
3. Dependency audit còn 6 high; license Vuexy/Mapbox/assets và một số notice texts chưa được xác nhận. Bản này không tự chấp thuận risk, license hoặc quyền phân phối.
4. Email Worker đã mặc định tắt worker và manual transport, hạn chế schema initialization vào Development, kiểm tra schema đã provision khi không initialize. 8/8 startup tests qua; intake thật/OCR/quyền thao tác/schema SQL được bàn giao chưa nghiệm thu. Xem [vận hành intake](EMAIL-INTAKE-OPERATIONS.md).
5. Các đầu vào thật còn thiếu: customer DB/PDF mapping, RPO/RTO/SLA, company authority/scanner, SMTP/TMS contracts/config, hosted registry/signing/scanning và UAT approval. EAP/OCR vẫn hoãn.

Không đưa token/config thật hoặc dữ liệu khách hàng vào bản xuất. Không chạy host, worker/email/task thật trong kiểm chứng này. Snapshot cộng tác trước thay đổi mốc 7 ngày đã được push main ở commit `d5193632edcbc14f0012caca8cd1c5c76f9a400f`; runtime mới đã push main ở87ddc9c; nhánh local `codex/das-completion-20261006` giữ cùng nguồn với main. Chạy Git từ `DAS-Collaboration`, xem `docs/LOCAL-WORKSPACE.md`. Không force push hoặc deploy. Script seed/tài khoản/dữ liệu cũ đã loại khỏi bản cộng tác.
# Repository chính thức — cập nhật 07/10/2026

Người dùng xác nhận repo làm việc chính thức là **https://github.com/Seleton-VN/Intern-DocumentAdministration-BE**. Chỉ push khi có yêu cầu rõ ràng, chỉ nội dung cần thiết trong phạm vi yêu cầu. Lượt này chỉ bàn giao database/hướng dẫn lên nhánh `database`; backend cũ được coi là tách biệt, không ghép hoặc sửa. Checkout bàn giao nằm tại `../DAS-Official-BE`; WIP phát triển vẫn giữ tại DAS-Collaboration. Remote origin cũ chưa đổi và không còn là nơi push mặc định. Quy định lâu dài nằm trong AGENTS.md ở root workspace.

> J20 ngày07/10: đã sửa PDF canvas local worker, overview/pagination VI/EN, Next script ownership, load lỗi quyền và UTC notification. Frontend379/379, targeted11/11, Notification19/19, năm trang PDF thật render qua, gateway25 thông báo có múi giờ UTC. Browser recheck sau sửa bị CUA policy chặn; chưa nghiệm thu toàn bộ J20/G7/G8. Không commit/push, EAP/OCR tiếp tục hoãn. Xem [báo cáo](J20-CORRECTION-REPORT-20261007.md) và [evidence](J20-CORRECTION-EVIDENCE-20261007.json).


## J20 browser recheck 07/10/2026
IAB truy cập được; sửa thêm PDF.js webpack entry và locale full-document navigation. 380/380 frontend tests, production build/typecheck và lint qua (0 errors/66 warnings). Đã kiểm PDF thật/download hash/fullscreen/keyboard, desktop/mobile/VI-EN, nhập Issued Date qua UI, report/Excel, notifications, My Staff gate, employee access denial và trang2 đối tác mobile. Xem J20-BROWSER-RECHECK-REPORT-20261007.md; còn runtime error cases và external contract gates, không tuyên bố J20 hoàn tất100%. Không commit/push.

## Nhóm 1 và 2 — cập nhật 08/10/2026

Đã hoàn tất phần J20 local còn mở sau lượt final gates07/10: browser reopen trên Brave được người dùng chọn, customizer mở/keyboard/focus, mobile PDF thật, VI/EN giữ query và trạng thái kiểm tra phiên ban đầu. Frontend **395/395**, typecheck0 lỗi, lint0 lỗi/66 warnings; không chạy production build mới hoặc SQL suite. Dossier QA mục tiêu/PDF storage giữ nguyên. Xem [báo cáo](GROUPS-1-2-REPORT-20261008.md), [evidence](GROUPS-1-2-EVIDENCE-20261008.json), [checkpoint hiện hành](GROUPS-1-2-CHECKPOINT-20261008.md).

Nhóm2 đã có [thiết kế J21–J23](contracts/J21-J23-README.md): 5GET đề xuất, 20schemas và38 tiêu chí nghiệm thu; **chưa triển khai endpoint**, ma trận vẫn CONTRACT_REQUIRED. J24 Deferred/External, G7 Partial/prepared, G8 chưa customerUAT/signoff. Không commit/push; EAP/OCR hoãn, Gemini MCP không gọi. Các số test/build/push trong đoạn trước là lịch sử, không phải trạng thái publish nguồn UI mới.

## Lượt triển khai J21–J23 ngày08/10/2026 — đang thực hiện

Sau lượt thiết kế, người dùng đã yêu cầu thực hiện nhóm1 và2. Theo dõi [checkpoint triển khai](J21-J23-IMPLEMENTATION-CHECKPOINT-20261008.md), không dùng đoạn thiết kế phía trên để kết luận endpoint mới còn hoàn toàn chưa viết. J21 backend/UI local đã review:68/68 backend targeted,29/29 frontend targeted và typecheck0. J22/J23 đang thực hiện; full build/gateway HTTP/browser cuối lượt chưa chạy. EAP/OCR hoãn, TMS/SMTP/authority/customer UAT vẫn là gate thật. Không commit/push.

## Dừng theo yêu cầu, lưu repo phụ — 09/10/2026

[Checkpoint dừng](STOP-CHECKPOINT-20261009.md): chốt Task1–3 (J21 backend/UI và J22 backend), Task4–7 pending. Người dùng cho phép đúng repo phụ dangthanhthi/Du_An_Luu_Tru_CongVan, nhánh codex/j21-j23-20261008; không push repo chính thức. Fresh frontend411/411, Document506/506, Notification19/19, Email11/11, J22SQL6/6; typecheck0/lint0errors66warnings và gatewayconfig4/4. Chưa productionfrontendbuild/browser mới/PythonlegacyQA đầy đủ/gatewaybuild; không deploy/realTMS/SMTP/UAT. Source review không thấy Critical/Important; Minor copy partner editor để lượt UI sau. Giữ cache/checkpoint và dừng sau bản lưu.


## Hoàn thiện J20–J23 local — 09/10/2026

Task1–7 đã triển khai backend/gateway/UI và kiểm local; đọc [checkpoint hiện hành](J21-J23-COMPLETION-CHECKPOINT-20261009.md). J22 có admin inactive list/reactivation; J23 có partner metadata timeline và V2 lifecycle history. J21 filter/paging đã kiểm bằng fixture; TMS thật vẫn unavailable. Browser kiểm DB QA/PDF thật, VI/EN/admin/reader/keyboard và responsive; sửa toolbar320px/menu ngang. Frontend479, Document544, Partner62, nativeSQL5, Python100 và gateway77 assertions đạt trong phạm vi checkpoint. Build/typecheck/lint đạt; không commit/push. G7 Partial/prepared, G8 chưa customer signoff; EAP/OCR hoãn, authority/TMS/SMTP/customer/deploy/license/security gates còn riêng.


## Chốt tài liệu và gói nhánh — 09/10/2026

Bản đồ TV1+TV3 cập nhật120 file; compilation/DI/namespace đã kiểm. Chuẩn bị60 file theo bốn nhánh,8 thay đổi thực; official checkout/refs giữ nguyên, không stage/commit/push. Docker source contexts/11 recipe tests đạt, chưa build/chạy image vì daemon dừng và RAM thấp. DAS preview giữ dừng theo yêu cầu giảm tác vụ nền. Xem [checkpoint mới](LOCAL-FINISHING-CHECKPOINT-20261009.md).

## Docker local đã kiểm và dừng — 09/10/2026

Theo quyền đóng Roblox/các ứng dụng trong lượt này, đã giảm tác vụ nền và kiểm Docker thật.6 backend +1 frontend image build đạt;6 backend startup/41 HTTP checks và25 frontend HTTP checks đạt.5 store SQLite mới chạy native SQL trong container, snapshot integrity/foreign keys đạt.15 layout và13 recipe tests qua; sửa công cụ view thiếu DTO mới và probe404 sau session gate, không đổi nghiệp vụ.1357 file nguồn sử dụng trong image được đối chiếu không drift, official checkout/refs giữ nguyên. Không commit/push/deploy, không nghiệm thu customer/SQL Server Production hoặc bật EAP/OCR/TMS/SMTP thật.

Docker/WSL đã dừng, cấu hình WSL tạm đã gỡ, resource smoke/build có owner đã dọn; giữ image local. Roblox/hình nền vẫn dừng; Zalo mở lại được giữ. Xem [checkpoint Docker hiện hành](DOCKER-EXECUTION-CHECKPOINT-20261009.md). G7 vẫn Partial/prepared và G8 chưa customer UAT/signoff; các gate bên ngoài theo checkpoint J21–J23.

## Cập nhật Git theo yêu cầu — 09/10/2026

Repo chính thức đã cập nhật đúng phạm vi: numbering `25f7e91` (README dependency), shared-contracts `7408872` (V2HistoryContracts/README/namespace), architecture `9d38bd1` (dependency/phương án ghép/bản đồ file). Database không có thay đổi cần xuất bản; main chính thức giữ nguyên `36f8f0f`. Đã đối chiếu refs trực tiếp trên GitHub; không đưa workflow/UI, tests, migration mới, checkpoint, credential, database/PDF hoặc cache vào bốn nhánh module.

Bản cập nhật repo phụ gồm75 file code/test/tài liệu kỹ thuật: J22/J23 backend/UI, read DTO/entity separation, lịch sử, responsive và công cụ QA. Kiểm chứng trước commit: frontend479/479; backend798 test qua trong8 project non-SQL,1 ca Restore SQL drill skipped vì không cấu hình môi trường riêng; layout15/15; Python QA102/102; typecheck exit0; lint0errors/66warnings cũ. Typecheck lần1 bị heap OOM1536MiB, lần2 với2048MiB qua; không đổi code hoặc bỏ bước kiểm kiểu. Không chạy lại Docker, SQL khách hàng, SMTP/TMS/EAP/OCR thật hoặc nghiệm thu Production. Các bản build, dữ liệu local và báo cáo ngoài phạm vi giữ ngoài commit.
