# Checkpoint dừng — rạng sáng 09/10/2026

Người dùng yêu cầu đẩy thay đổi cần thiết lên **repo phụ** rồi nghỉ. Đích duy nhất được phép trong lượt này: `https://github.com/dangthanhthi/Du_An_Luu_Tru_CongVan`, nhánh `codex/j21-j23-20261008`. Không push repo làm việc chính `Seleton-VN/Intern-DocumentAdministration-BE`. Các lượt sau vẫn phải có yêu cầu commit/push mới.

## Đã chốt và phần chưa làm

| Phần | Trạng thái chính xác |
|---|---|
| J21 backend và UI My Staff | Task1–2 đã triển khai và review PASS. Lọc assignee trên server, hai phân trang riêng, request/session ownership, TMS degraded với count chưa biết. TMS thật và browser/gateway của bản mới chưa nghiệm thu. |
| J22 backend | Task3 đã triển khai và review spec/code quality PASS. Admin GET/options, active/inactive/all, search literal, paging và snapshot dưới EF retry. Giữ active-only lookup và quyền CatalogManage. |
| J22 UI | Task4 chưa triển khai: options authoritative, activity/search/paging, kích hoạt lại cùngID/code, xử lý conflict và browser. |
| J23 partner audit | Task5 chưa triển khai: watermark Int64 string, metadata-only, soft-delete history và đúng quyền. |
| J23 document lifecycle | Task6 chưa triển khai: lấy lý do từng chu kỳ từ event audit, restore đúng trạng thái, parser/authority/watermark. |
| J23 UI và nghiệm thu bản mới | Task7 chưa triển khai: deep links/timeline, production frontend build, runtime/gateway HTTP/browser mobile/desktop/VI/EN/keyboard. |

Các route gateway task/options/catalog GET đã bổ sung và test cấu hình có; route history là chuẩn bị trước, **chưa có controller/UI và chưa phải API đã nghiệm thu**. OpenAPI/spec của J23 là kế hoạch, không chứng minh tính năng chạy.

## Kiểm chứng phục vụ bản lưu

- Frontend: **411/411**, 0fail/skip. Typecheck0 trên cùng source frontend; targetedlint0. Full lint0errors/66warnings; gateway config4/4. PythonlegacyQA đầy đủ còn pending: entry trực tiếp không đúng layout, compatibilityview quétcache quá lâu nên dừng theo yêu cầu lưu tạm. Không nhận các ca lỗi môi trường thành pass.
- Document Service: **506/506**, Notification: **19/19**, Email Worker: **11/11**; lệnh chọn loại trừ tên test chứaSql, không tính SQLskip thành pass.
- J22 native SQL: **6/6**, gồm3mới+3catalog regression, có retry/read consistency/reactivation race; actual query plan lưu private. Không migration/index mới, không sửa database/settings có sẵn.
- Review bản lưu: không thấy Critical/Important; Minor còn lại ở banner lỗi lưu đối tác nói tạo mới dù đang cập nhật. Mutation vẫn khóa chờ reload; sửa copy ở lượt UI sau.
- Email compile ban đầu hỏng do `obj/project.assets.json` trỏ cacheNuGet export đã dọn. `dotnet restore --force --disable-parallel` tạo lại dependency assets và test11/11qua; không sửa source để che lỗi.
- Chưa chạy production frontend build/browser mới. Không coi bản lưu là deploy, UAT hoặc toàn bộ G7 hoàn tất.

## Resume chính xác

Làm tại `DAS-Collaboration/`, nhánhlocal trên. Baseline trước lượt làm: `ec94a448f4e05f66c47b1342701637f88688a129`; commit hiện tại xem `git log`. Giữ WIP/báo cáo local khác, không reset/stash/clean.

1. Đọc [checkpoint triển khai](J21-J23-IMPLEMENTATION-CHECKPOINT-20261008.md), [kế hoạch](superpowers/plans/2026-10-08-j21-j23-contracts.md), [specs](contracts/J21-J23-README.md).
2. Local cache: `.artifacts/qa/j21-j23-implementation-20261008-01/` có progress.md, baseline1272files, task1–7briefs, task1–3reports/reviews, red/green/TRX, publication scope/review/receipt. **Không dispatch lại Task1–3.** Bắt đầu Task4 sau đối chiếu ledger.
3. Chạy tuần tự để giảm RAM. Không dừng ứng dụng người dùng/Roblox/WSL. Không tự mở worker/mail/TMS thật. Dịch vụ preview DAS hiện dừng; khi cần kiểm browser, rebuild/restart đúng owner bằng private previewhost cùng SQLiteQA/PDF storage.
4. Task6 cần kiểm edgecase32000 ký tựJSON chứa hai lý do4000Unicode; chưa chứng minh bằngSQL nên chưa đổi schema hoặc rút ngắn lý do. Ghi RED rồi chỉ sửa nếu có bằng chứng.
5. TMS production vẫn unavailable; positive tasks chỉ có fixture boundary tests. Không thay bằng task giả trên UI. Browser Brave được người dùng cho phép trước đó; chọn lại surface đúng trạng thái hiện tại.
6. EAP do nhóm khác, OCR hoãn, Gemini MCP đã gỡ. Reminder **quá7ngày, thứHai08:00VN**, năm triển khai2027, số thứ tự4chữsố chỉ thành5khi vượt9999, restore về trạng thái trước hủy.

## Đầu vào còn thiếu và giới hạn

TMS auth/sandbox/IDmapping/filter-before-page; authority/capability/scanner thật; SMTP account/recipient; customer DB/catalog/partner/PDF mapping; production server/SQL/storage/TLS/RPO/RTO/SLA; người UAT/pilot/signoff. Fax cần chốt phạm vi/contract. EAP là trách nhiệm nhóm khác. **G7 Partial/prepared, G8 chưa nghiệm thu**.

Bản xuất chỉ code/tests/schema migrations/tài liệu cần thiết. Credential, cấu hìnhlocal, customerDB/PDF, cache/build/node_modules và báo cáo ngoài phạm vi giữ ngoài Git. Đây là ngoại lệ pushrepo phụ cho lượt này, không đổi repo chính thức mặc định.
