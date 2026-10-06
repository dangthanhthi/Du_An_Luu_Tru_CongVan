# Tiến độ và phạm vi bản cộng tác

Bản cộng tác ngày 06/10/2026. G7 **Partial/prepared**; G8 chưa UAT/signoff. Không dùng số giai đoạn hoặc số lượt test để suy tỷ lệ hoàn thành dự án. Ước lượng 70% trước đây không có mẫu số được duyệt; theo dõi bằng phần đã kiểm và gate còn mở.

## Nguồn trước khi sắp xếp

Linux Node 22: 214/214 frontend tests, typecheck/lint/full build/source integrity qua. Python QA 57/57. Image source tương ứng: 25 HTTP checks, 77 template paths và 6 kiểm tra DOM/title/menu ẩn danh qua. Backend 608 non-SQL và các SQL/restore/startup/load checks có bằng chứng ở nguồn phát triển trước đó, theo scope giả lập riêng. Những số này **chưa tự chứng minh layout mới**.

Kiểm tra sau sắp xếp đã qua: **608/608 backend non-SQL tests và Gateway Release build; 214/214 frontend tests, typecheck, lint (0 lỗi/66 warnings), full build; 10/10 layout và 57/57 Python QA tests**. Linux Node22/.NET10, nguồn cố định, source integrity và cleanup qua. 1,532 file đầu vào build đối chiếu byte với cây Git đều khớp. Backend inputs không đổi so với lượt backend đã qua; các chỉnh sửa tiếp theo chỉ ở build Prisma frontend/công cụ kiểm. Xem [bằng chứng](PUBLICATION-VERIFICATION.json). Đây là kiểm tra cục bộ và Git source archive; hosted clean-clone CI, Windows frontend, live gateway/authority/customer migration/UAT chưa được chấp nhận.

## Phần đã triển khai nội bộ

| Giai đoạn | Phần đã có | Còn chờ |
|---|---|---|
| G0/G1 | Baseline, contracts, master data/company/phòng/đối tác, CRUD/audit/restore | Mapping và nguồn chuẩn EAP/legacy |
| G2 | Backend policies/capability/fail-closed, credential login adapter, refresh/logout ownership guards | Delayed login ownership bug; browser→gateway→authority/session/CSRF/revocation thật |
| G3 | 3 loại công văn/counters, register/edit/distribute/cancel/restore/relations/PDF/idempotency/audit | File/scanner/live authority và UAT |
| G4 | Durable notification inbox/outbox/dedup/retry/history | SMTP thật; EAP/OCR hoãn; Fax sau |
| G5 | Dashboard/report/XLSX cùng scope, reminder14ngày/thứHai08VN | Audience/directory/SMTP config thật |
| G6 | DAS My Staff/tasks, task intent/retry/reconcile/history | TMS contract/sandbox/hierarchy/ID mapping và authority thật |
| G7 | Preflight, CI/image/runtime/config, Production DB checks, restore 5 DB+PDF, load/idempotency, runbooks | Preflight schema/PDF bugs; customer export/mapping/RPO/RTO/SLA; security/license; hostedCI/registry/signing/scanning |
| G8 | 18 kịch bản UAT đã chuẩn bị | Môi trường/dữ liệu thật, mentor chạy và ký duyệt/pilot |

## Backlog cần đọc trước khi sửa

1. G2: authApi.login có thể hoàn tất sau local logout hoặc failure cũ xóa phiên mới. Đã có probe trên nguồn phát triển; cần ownership guard và delayed-response tests. Refresh/logout guards hiện có không giải quyết tất cả login/account-switch/UI races hoặc cross-tab atomic rotation.
2. G7: audit-migration-export.py chưa reject top-level nonobject đúng dạng report, bool counter year hoặc non-PDF bytes khi hash/size khớp. Cần RED/GREEN và giữ read-only semantics, không giả lập customer migration là đạt.
3. Dependency audit còn 6 high; license Vuexy/Mapbox/assets và một số notice texts chưa được xác nhận. Bản này không tự chấp thuận risk, license hoặc quyền phân phối.
4. Email Worker cũ còn gọi EnsureCreated và đăng ký background worker khi startup; chưa được chấp nhận để bật trên Production. Hai OCR client/interface cũ được giữ chỉ để biên dịch dependency hiện có; OCR service vẫn hoãn.
5. Các đầu vào thật còn thiếu: customer DB/PDF mapping, RPO/RTO/SLA, company authority/scanner, SMTP/TMS contracts/config, hosted registry/signing/scanning và UAT approval. EAP/OCR vẫn hoãn.

Không đưa token/config thật hoặc dữ liệu khách hàng vào bản xuất. Không chạy host, worker/email/task thật trong kiểm chứng này. Theo yêu cầu mới, snapshot cộng tác được push trực tiếp lên main sau kiểm tra; không force push hoặc deploy. Script seed/tài khoản/dữ liệu cũ đã loại khỏi bản cộng tác.
