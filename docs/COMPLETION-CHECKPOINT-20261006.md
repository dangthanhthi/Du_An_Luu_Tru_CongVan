# DAS — checkpoint hoàn thiện ngày 06/10/2026

> Cập nhật GitHub/hosted CI: runtime `abb54a9` đã lên main; backend629 mỗi OS, frontend223, SQL77 và layout/QA qua trên clean checkout GitHub. Full workflow failure do audit6high; không tự nghiệm thu security/license. Xem [checkpoint hosted](HOSTED-CI-CHECKPOINT-20261006.md).

> SQL QA canonical đã qua **75/75 core + load + restore** trên instance riêng, bundle 5 DB/PDF qua checksum, source integrity và cleanup qua; Python QA hiện hành **93/93**, layout **10/10**. Xem [checkpoint SQL](SQL-CHECKPOINT-20261006.md) và [evidence SQL](SQL-VERIFICATION-20261006.json). CI SQL manual đã chuẩn bị; customer SQL/migration, hosted CI và các gate thật chưa được nghiệm thu.

> Cập nhật sau commit `bac9c4b`: preflight CLI đã chặn overwrite/hardlink/traversal/ADS và xử lý JSON lỗi; **26/26 Windows, 26/26 Linux**, Python QA hiện hành **79/79**, layout **10/10**. Xem [checkpoint preflight](PREFLIGHT-CHECKPOINT-20261006.md) và [evidence](PREFLIGHT-VERIFICATION-20261006.json). Kết quả 67 Python tests dưới đây là mốc trước bổ sung CLI.

> Frontend Windows Node22 đã kiểm xong trên commit nguồn `cfde434`: **223/223 tests**, typecheck/lint/full build và source integrity qua. Session81113 terminal exit0. Xem [checkpoint Windows](WINDOWS-FRONTEND-CHECKPOINT-20261006.md); file handover đang chạy trước đây giữ vai trò lịch sử.

## Quyết định mới và phạm vi

- Người dùng xác nhận chính thức: nhắc công văn **quá 7 ngày**, thay thế mốc 14 ngày trước đây. Giữ cách tính ngày lịch Việt Nam, lịch thứ Hai 08:00 Việt Nam, nhóm công văn đi/nội bộ chưa hoàn tất, loại Cancelled/Incoming; Queued/Accepted chưa phải Sent. Đúng 7 ngày chưa đủ điều kiện, ngày thứ 8 mới đủ điều kiện.
- EAP do người khác phụ trách. Không triển khai EAP, SSO EAP, RabbitMQ EAP hoặc NetBird; chỉ mở lại khi người dùng yêu cầu rõ ràng. OCR tiếp tục hoãn.
- Người dùng cho phép tự quyết phương án tối ưu và ghi lại; tiếp tục các việc DAS có thể làm với đầu vào hiện có. Không giả lập dữ liệu thật hoặc tự ký nghiệm thu.
- Nguồn code: `DAS-Collaboration`, nhánh `codex/das-completion-20261006`; không phát triển lệch ở worktree cũ. Mốc GitHub main trước thay đổi là `d5193632edcbc14f0012caca8cd1c5c76f9a400f`.

## Thứ tự công việc

1. [x] Đổi mốc 7 ngày ở eligibility, truy vấn báo cáo, cảnh báo quyền, nội dung email và UI. Kiểm thử RED: 13/36 lỗi với code 14 ngày; GREEN: 36/36 qua trên Windows .NET10.0.204. Lịch thứ Hai 08:00 giữ nguyên. Bằng chứng local: `.artifacts/qa/completion-20261006/reminder-{baseline,red,green}`.
2. [x] Sửa quyền ghi phiên của login trả về muộn; chỉ yêu cầu hiện hành được lưu/clear phiên, lỗi ghi storage phải rollback phiên dở dang. 34/34 focused tests qua, 6 kiểm thử concurrency đã thất bại với code cũ; không khẳng định khóa liên tab hoặc BFF đã hoàn tất.
3. [x] Hoàn thiện preflight export/PDF chỉ đọc: schema, năm/bool, metadata/path/link/signature/hash/size; giữ số công văn lịch sử. RED 31 subtest failures/5 errors, GREEN 14/14 không skip; Python QA compatibility view 67/67 qua.
4. [x] Email Worker: bỏ khởi tạo DB tự động trong Production và giữ worker/scan thật tắt khi chưa bật có chủ đích; không nối SMTP/EAP/OCR. RED 7/8 lỗi, GREEN 8/8 qua. Database provider/connection explicit; mặc định Initialize=false, worker=false, manual=false. SQL schema thật vẫn cần bàn giao.
5. [x] Loại bỏ quyền công văn tự động của Admin ở API legacy, phù hợp quy tắc mentor. Từ chối user ID rỗng và không coi department ID rỗng là membership. RED: 7/18 authorization tests lỗi với bypass cũ; GREEN các nhóm liên quan 35/35; HTTP và authorization bổ sung 22/22 qua sau sửa ID rỗng. Review độc lập không phát hiện lỗi actionable. Không thay đổi số hoặc quyền nghiệp vụ hợp lệ của công văn cũ.
6. [x] Kiểm tra tổng hợp bản sửa đã qua: Linux backend 629/629 non-SQL tests và Gateway Release build; Linux frontend 223/223 tests, typecheck/lint/full build; Python QA 67/67 và layout 10/10. Đã rà audit, giữ các gate bảo mật/license còn mở. Bằng chứng [COMPLETION-VERIFICATION-20261006.json](COMPLETION-VERIFICATION-20261006.json).

## Kết quả kiểm chứng hiện hành

- Backend: `backend-linux-v3`, snapshot SHA256 `6f463516308efc8529e4ca3c7975af757f1fa303d31737b1992326ce0da25ebd`; Document406, Files82, Partner46, Auth54, Notification20, Email11, PdfIntegration8, ReminderIntegration2. Không có failure/skip, Gateway build qua. Đối chiếu 340 file nguồn backend/workflow/database/công cụ chạy với nguồn hiện hành khớp byte.
- Frontend: `web-linux-v2`, snapshot SHA256 `5efb4e8b233370229476b7fec266ee3a8bf79c27770f36a9b690b99d21b18cd2`; 223 tests, typecheck/lint/full build qua. Đối chiếu 1.158 file frontend/Prisma/công cụ chạy với nguồn hiện hành khớp byte; thay đổi backend sau đó không thay các đầu vào này.
- Linux QA dùng Node22.23.3, .NET10.0.401, Python3.12.3; image `sha256:cb55328caaecd13556e86c4cca2e0406f0ef6a743005c4772ae02563644b3fa7`, nonroot, không mount socket/credential, không mở port, không bật worker. Source integrity và cleanup container/volume qua.
- Python QA hiện hành: `python-qa-final` 67/67; layout `layout-final-confirmed.log` 10/10. Đã sửa công cụ cập nhật manifest ghi UTF-8/LF trên Windows để giữ gate layout; original import hashes được giữ.
- Đây là bằng chứng chức năng cục bộ; SQL giả lập trên instance SQL thật đã có checkpoint riêng nêu trên. **Không chứng minh SQL/customer migration/live gateway/authority/SMTP/TMS/Fax/UAT Production**. G7 vẫn Partial/prepared, G8 chưa chạy/signoff. Mốc 608/214/57 và Windows619 trước sửa Admin chỉ là lịch sử.

## Các phần còn chờ

TMS API task/sandbox; SMTP và người nhận thật; bàn giao directory/quyền từ người phụ trách; dữ liệu DB/PDF khách hàng và mapping; scanner/storage đích; môi trường triển khai/backup RPO/RTO/SLA; license và UAT/pilot/signoff. Fax vẫn chưa hoàn thiện luồng thật. Không coi các phần này đã hoàn tất vì các kiểm thử giả lập qua.

Các kết quả 608/214/57 và image ở publication trước là lịch sử; không dùng để chứng minh toàn bộ nguồn sau thay đổi này.

Review độc lập read-only `/root/review_das_completion` đã đọc diff và hai file mới, không thấy lỗi correctness cần sửa trong phạm vi trên. Reviewer không chạy test hoặc service. Gemini background job `f03287ba910445f69f4625aef44904d6` thất bại 503 cả hai tài khoản; không có kết quả advisory và không retry lặp.

## Quyết định kỹ thuật và bằng chứng giữ lại

- Dùng một hằng số 7 ngày cho eligibility, báo cáo, cảnh báo quyền và nội dung email; UI hiển thị 7 ngày. Không đổi lịch tuần hoặc tiêu chí trạng thái. Không có worker gửi thư thật trong QA.
- Login chỉ lưu phiên nếu request còn sở hữu phiên; lỗi ghi storage hủy phiên ghi dở dang. Không tuyên bố thao tác localStorage là transaction liên tab hoặc đã có BFF.
- Preflight migration chỉ đọc, không import hoặc cấp lại số. PDF kiểm chữ ký, size/hash/path; scanner Production vẫn là gate riêng.
- Email intake mặc định không quét tự động và không gọi transport qua endpoint thủ công. SQL Production phải provision schema riêng; không đưa SQLite/EnsureCreated vào Production.
- Linux web lượt đầu thiếu executable `npm` để Next.js tải SWC fallback. Công cụ QA đã thêm shim gọi đúng npm CLI chính thức trong môi trường tạm; giữ nguyên app source/lockfile. Lượt sau build qua, nguồn không bị sửa và container/volume đã dọn.
- Linux backend lượt đầu sau sửa Admin: 405/406 Document tests qua; một test isolation V2 còn kỳ vọng Admin được tạo Incoming. Đã thêm assertion Admin bị từ chối và kiểm tra collision V2 bằng SecretaryDirector có quyền; focused test qua. Log lượt thất bại được giữ nguyên, không dùng lượt đó làm bằng chứng backend đã qua.
- Audit npm lockfile hiện tại: 6 high ở dev dependency chain, runtime audit không có finding. Advisory [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) chưa có patched version được xác minh. Không áp dụng major downgrade do audit gợi ý; security gate và license vẫn mở, không tự chấp nhận rủi ro.
- Giữ raw logs/TRX/source snapshots trong `.artifacts/qa/completion-20261006`, ngoài Git. Chỉ mã, test, runbook và báo cáo tổng hợp liên quan DAS được đưa vào commit local.

## Tiếp tục phiên sau

1. Mở repo `C:/Users/MSIIIIII/Desktop/Dự án taskmanager/DAS-Collaboration`; đọc checkpoint này, `docs/COLLABORATION-STATUS.md`, `docs/BUSINESS-RULES.md` và báo cáo verification mới trước khi sửa.
2. Kiểm tra `git status` và `git log -1`. Nhánh local hiện tại là `codex/das-completion-20261006`, GitHub main mốc cũ là `d5193632edcbc14f0012caca8cd1c5c76f9a400f`. Không phát triển song song trong worktree lịch sử.
3. Giữ EAP ngoài phạm vi và OCR hoãn. Tài liệu/credential Task Management không phải credential DAS; không tái sử dụng.
4. Khi có đầu vào thật, đối chiếu owner/scope/ID mapping và cấu hình trước khi nối SMTP/TMS/scanner/customer migration. Chạy kiểm chứng theo gate với dữ liệu được phép; chỉ coi Sent khi transport xác nhận. UAT cần mentor chạy và ký.
5. Với thông tin hiện tại, G7 vẫn Partial/prepared và G8 chưa nghiệm thu. Không suy tỷ lệ từ số test hoặc tự tạo dữ liệu/config để đóng các gate còn thiếu.
