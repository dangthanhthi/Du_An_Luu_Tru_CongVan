# Checkpoint triển khai J21–J23 — 08/10/2026

Trạng thái: **PAUSING_AT_USER_REQUEST**. Người dùng yêu cầu lưu thay đổi lên repo phụ rồi nghỉ; chốt đến Task3 và để Task4–7 lần sau. Đây là lượt triển khai mới sau lượt hoàn thành thiết kế cùng ngày; không dùng báo cáo thiết kế để kết luận API đã triển khai.

## Nguồn và ràng buộc

`DAS-Collaboration`, nhánh local `codex/j21-j23-20261008`, baseline HEAD `ec94a448f4e05f66c47b1342701637f88688a129`. Yêu cầu mới cho phép commit/push bản lưu đúng repo phụ `dangthanhthi/Du_An_Luu_Tru_CongVan`, nhánh `codex/j21-j23-20261008`; không push repo chính thức. Ngoài lượt này vẫn chỉ commit/push khi người dùng yêu cầu. Giữ WIP khác. Official repository vẫn Seleton-VN/Intern-DocumentAdministration-BE; checkout bàn giao database tách riêng, không ghép backend cũ.

EAP do nhóm khác, OCR hoãn, không gọi Gemini MCP. Reminder >7ngày/thứHai08:00 Việt Nam. API TMS/SMTP thật chưa được cấp cho DAS; không suy rằng test fixture đã nối hệ thống thật.

## Tiến độ hiện tại

| Task | Trạng thái | Bằng chứng |
|---|---|---|
| 1 Backend My Staff | Đã triển khai, review spec/code quality APPROVED sau sửa header401 | 68/68 targeted tests:57mới+11legacy, red/green; `task-1-report.md`, `task-1-rereview.md` |
| 2 Frontend My Staff | Đã triển khai, review spec/code quality PASS | 29/29 targeted tests, lint0/typecheck0; browser/build cuối lượt còn pending |
| 3 Backend catalog inactive | Đã triển khai, review spec/code quality PASS | 58/58 HTTP/SQLite +6/6 native SQL; không migration; browser/UI còn pending |
| 4 Frontend catalog inactive | Chưa triển khai | Spec/kế hoạch đã có |
| 5 Partner audit | Chưa triển khai | Metadata-only, không dựng before/after |
| 6 Document lifecycle history | Chưa triển khai | Event-owned cancellation reason, V2LifecycleOnly |
| 7 History UI/final build/browser | Chưa triển khai/kiểm cuối | Root tích hợp gateway và chạy checks tuần tự |

Gateway đã bổ sung route task/options/history và GET catalog root; kiểm registration config4/4 qua sau4ca đỏ. **Chưa phải HTTP gateway acceptance**: preview vẫn dùng Release DLL cũ, cần rebuild/restart có đối chiếu ownership sau khi xong code.

## Quyết định kỹ thuật đã thực hiện

- Task filtering thu hẹp allowed IDs trước gọi connector, không filter page đã tải ở client. Resolve lại authority sau TMS; scope đổi trả409 và không phát payload cũ. TMS không phục vụ trả degraded/tasks=null, không dựng danh sách rỗng thành công.
- Kiểm count/page từ chối `offset+items.Count > total`; không yêu cầu mọi page đầy, nhưng không nhận terminal page bất khả thi.
- Header no-store được đặt trước auth cho các path `/api` bằng OnStarting trong document-service; actual expired/missing JWT401 đã kiểm. Không đổi policy/token/authority.
- SQL read snapshot phải đi qua EF execution strategy khi SQL retry được bật. Không tự bật RCSI hoặc sửa SQL settings toàn máy; không giữ transaction qua lời gọi authority từ xa.
- Review so baseline byte snapshot trước lượt làm, không so HEAD rồi nhận cả WIP cũ là code mới.

## Resume và môi trường

- Ledger/baseline/briefs/reports/logs: `.artifacts/qa/j21-j23-implementation-20261008-01/`. Baseline1272 source text files; không xóa hoặc ghi đè trong khi review. Các task marked complete trong `progress.md` không được dispatch lại từ đầu.
- Task3 implementer đã dừng sau report; đang chốt review bản xuất và kiểm cuối trước push. Lần sau đọc ledger, không dispatch lại Task1–3. Không chạy full-suite/build song song với worker đang test. RAM máy8GB, giữ Roblox và ứng dụng người dùng nguyên trạng.
- Frontend3211 đã tạm dừng sau xác minh launcher/server thuộc đúng DAS để chuẩn bị build. J20Host5002–5004 và gateway8080 cũng tạm dừng sau xác minh owner để giảm RAM; inventory hiện tại không còn listener DAS nào; dữ liệu SQLite/PDF giữ nguyên, cuối lượt cần restart toàn bộ preview. Trong lượt dừng này không mở lại dịch vụ; lần sau rebuild/restart preview và kiểm browser Brave đã được người dùng chọn.
- Mỗi task phải có red/green, review spec/code quality và sửa các finding trước đánh dấu hoàn tất. Kế hoạch [triển khai](superpowers/plans/2026-10-08-j21-j23-contracts.md), specs [mục lục](contracts/J21-J23-README.md).
- Kiểm cuối phục vụ bản lưu: frontend411/411, Document506/506, Notification19/19 đã qua. Email11/11qua, typecheck0, lint0errors/66warnings, gateway config4/4. PythonlegacyQA đầy đủ/gateway build pending trong bản lưu tạm. Production frontend build và browser từng route mới còn pending. G7/G8 vẫn giữ customer/UAT/real integration gates.

## Đầu vào còn thiếu để nghiệm thu thật

Người dùng hỏi ngày08/10/2026 trong lúc đang triển khai; đây là gate tích hợp/triển khai, không ngăn hoàn thiện code local:

| Đầu vào | Cần xác nhận | Không được suy từ test local |
|---|---|---|
| TMS | Phiên bản API thực tế, auth/sandbox, ID mapping, allowed-assignee filtering trước count/page và thứ tự ổn định | Tasks fixture không chứng minh TMS thật |
| Authority/capability/scanner | Actor active, cây quản lý/phòng chính, nguồn CatalogManage và scanner PDF được công ty chấp nhận | Local authority/grant/scanner không phải quyền hoặc scanner production |
| SMTP | Server/TLS/account gửi, người nhận nhắc và chính sách gửi; cung cấp qua config riêng, không đưa secret vào chat/Git | Worker disabled/test outbox không chứng minh gửi mail thật |
| Dữ liệu khách hàng | Source DB/schema, bộ danh mục/đối tác chính thức, legacy ID/PDF mapping và dữ liệu đối chiếu migration | Schema/migration/QA PDF pass không thay kiểm dữ liệu thật |
| Production và vận hành | Server/SQL/storage/domain/TLS, backup RPO/RTO, SLA và registry/signing/scanning/license nếu áp dụng | Build local không phải deploy hoặc restore production |
| UAT/signoff | Người nghiệm thu, bộ ca và dữ liệu chuẩn, kết quả pilot/chấp nhận; Fax còn cần xác định phạm vi/contract khi đưa vào | G7 Partial/prepared và G8 chưa signoff vẫn giữ |

EAP là trách nhiệm nhóm khác và vẫn hoãn đến khi người dùng yêu cầu; OCR hoãn. Những thông tin nghiệp vụ đã chốt (>7ngày, thứHai08:00VN, năm2027, số4chữsố chỉ tăng5 khi vượt9999, restore về trạng thái trước hủy) không hỏi lại.
