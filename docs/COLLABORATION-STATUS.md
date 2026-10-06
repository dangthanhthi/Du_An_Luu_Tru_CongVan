# Tiến độ và phạm vi bản cộng tác

## Phân công EAP — cập nhật ngày 06/10/2026

Theo yêu cầu người dùng, **EAP do người khác phụ trách; Codex không triển khai EAP cho đến khi người dùng yêu cầu rõ ràng mở lại phần này**. Yêu cầu chung “tiếp tục dự án” chỉ tiếp tục phần DAS, không mở lại EAP.

Phạm vi EAP được loại khỏi công việc hiện tại của Codex gồm kết nối SSO/Identity EAP, API tổ chức EAP, đồng bộ người dùng EAP qua RabbitMQ và thiết lập NetBird phục vụ EAP. My Staff và nghiệp vụ task trong DAS vẫn thuộc phạm vi DAS; dữ liệu/quyền/mapping phụ thuộc EAP chờ người phụ trách bàn giao, không tự giả định đã có tích hợp thật.

Tài liệu và cấu hình mentor mới cung cấp mang tên Task Management; chưa xác nhận client/credential được cấp cho DAS. Không sử dụng credential đó hoặc đưa file `.env` vào Git/frontend. OCR vẫn hoãn theo yêu cầu trước.

Bản cộng tác ngày 06/10/2026. G7 **Partial/prepared**; G8 chưa UAT/signoff. Không dùng số giai đoạn hoặc số lượt test để suy tỷ lệ hoàn thành dự án. Ước lượng 70% trước đây không có mẫu số được duyệt; theo dõi bằng phần đã kiểm và gate còn mở.

## Nguồn trước khi sắp xếp

Linux Node 22: 214/214 frontend tests, typecheck/lint/full build/source integrity qua. Python QA 57/57. Image source tương ứng: 25 HTTP checks, 77 template paths và 6 kiểm tra DOM/title/menu ẩn danh qua. Backend 608 non-SQL và các SQL/restore/startup/load checks có bằng chứng ở nguồn phát triển trước đó, theo scope giả lập riêng. Những số này **chưa tự chứng minh layout mới**.

Kiểm tra sau sắp xếp đã qua: **608/608 backend non-SQL tests và Gateway Release build; 214/214 frontend tests, typecheck, lint (0 lỗi/66 warnings), full build; 10/10 layout và 57/57 Python QA tests**. Linux Node22/.NET10, nguồn cố định, source integrity và cleanup qua. 1,532 file đầu vào build đối chiếu byte với cây Git đều khớp. Backend inputs không đổi so với lượt backend đã qua; các chỉnh sửa tiếp theo chỉ ở build Prisma frontend/công cụ kiểm. Xem [bằng chứng](PUBLICATION-VERIFICATION.json). Đây là kiểm tra cục bộ và Git source archive; hosted clean-clone CI, Windows frontend, live gateway/authority/customer migration/UAT chưa được chấp nhận.

## Phần đã triển khai nội bộ

Cập nhật sau xác nhận **quá 7 ngày**: backend Linux **629/629 non-SQL**, Gateway Release build; frontend Linux **223/223**, typecheck/lint/full build; Python QA **67/67** và layout **10/10** đã qua. Nguồn đầu vào đã đối chiếu hash với cây hiện hành; source integrity/cleanup qua. Đã sửa login ownership, preflight PDF, Email Worker startup gates và quyền Admin legacy. Xem [checkpoint](COMPLETION-CHECKPOINT-20261006.md) và [bằng chứng mới](COMPLETION-VERIFICATION-20261006.json). Các gate thật dưới đây vẫn mở; những số này không thay UAT hoặc chấp thuận bảo mật/license.

| Giai đoạn | Phần đã có | Còn chờ |
|---|---|---|
| G0/G1 | Baseline, contracts, master data/company/phòng/đối tác, CRUD/audit/restore | Mapping và nguồn chuẩn EAP/legacy |
| G2 | Backend policies/capability/fail-closed, credential login adapter, refresh/logout/login ownership guards và rollback lỗi lưu phiên | browser→gateway→authority/session/CSRF/revocation thật |
| G3 | 3 loại công văn/counters, register/edit/distribute/cancel/restore/relations/PDF/idempotency/audit | File/scanner/live authority và UAT |
| G4 | Durable notification inbox/outbox/dedup/retry/history | SMTP thật; EAP/OCR hoãn; Fax sau |
| G5 | Dashboard/report/XLSX cùng scope, reminder7ngày/thứHai08VN | Audience/directory/SMTP config thật |
| G6 | DAS My Staff/tasks, task intent/retry/reconcile/history | TMS contract/sandbox/hierarchy/ID mapping và authority thật |
| G7 | Preflight đã siết schema/PDF, CI/image/runtime/config, Production DB checks, restore 5 DB+PDF, load/idempotency, runbooks | customer export/mapping/RPO/RTO/SLA; security/license; hostedCI/registry/signing/scanning |
| G8 | 18 kịch bản UAT đã chuẩn bị | Môi trường/dữ liệu thật, mentor chạy và ký duyệt/pilot |

## Backlog cần đọc trước khi sửa

1. G2: Đã sửa login trả về muộn/overlap và rollback phiên dở dang khi storage lỗi; 34/34 focused tests qua trên Windows. Cross-tab atomic rotation, BFF/session/authority thật và nghiệm thu browser/gateway còn mở.
   Đã bỏ bypass Admin ở API công văn legacy; Admin chỉ có quyền quản trị master data, quyền công văn theo vai trò/phạm vi nghiệp vụ. Các bài test cũ dùng Admin để tạo công văn đã chuyển sang vai trò thư ký phù hợp. Kiểm tra HTTP xác nhận list/detail/file/mutation không tiết lộ hoặc thay đổi công văn cho Admin không có quyền nghiệp vụ.
2. G7: Đã sửa top-level nonobject, bool/year, PDF metadata/path/link/signature/hash/size; 14/14 focused tests qua, giữ read-only semantics. Đây chỉ là preflight, chưa nhập dữ liệu khách hàng hay thay thế scanner.
3. Dependency audit còn 6 high; license Vuexy/Mapbox/assets và một số notice texts chưa được xác nhận. Bản này không tự chấp thuận risk, license hoặc quyền phân phối.
4. Email Worker đã mặc định tắt worker và manual transport, hạn chế schema initialization vào Development, kiểm tra schema đã provision khi không initialize. 8/8 startup tests qua; intake thật/OCR/quyền thao tác/schema SQL được bàn giao chưa nghiệm thu. Xem [vận hành intake](EMAIL-INTAKE-OPERATIONS.md).
5. Các đầu vào thật còn thiếu: customer DB/PDF mapping, RPO/RTO/SLA, company authority/scanner, SMTP/TMS contracts/config, hosted registry/signing/scanning và UAT approval. EAP/OCR vẫn hoãn.

Không đưa token/config thật hoặc dữ liệu khách hàng vào bản xuất. Không chạy host, worker/email/task thật trong kiểm chứng này. Snapshot cộng tác trước thay đổi mốc 7 ngày đã được push main ở commit `d5193632edcbc14f0012caca8cd1c5c76f9a400f`; các sửa tiếp theo đang ở nhánh local `codex/das-completion-20261006`. Không force push hoặc deploy. Script seed/tài khoản/dữ liệu cũ đã loại khỏi bản cộng tác.
