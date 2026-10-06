# Tiến độ và phạm vi bản cộng tác

> Xác nhận của người dùng: **chưa đủ thông tin dữ liệu khách hàng và chưa kiểm chứng trên môi trường thật**. G7 giữ trạng thái **Partial/prepared**, chưa nghiệm thu; G8 chưa UAT/signoff. Ước lượng 70–80% chỉ nói về phần chuẩn bị kỹ thuật, không phải tỷ lệ hoàn thành toàn bộ G7. Chưa có trọng số được duyệt để báo một tỷ lệ nghiệm thu đáng tin cậy.

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
