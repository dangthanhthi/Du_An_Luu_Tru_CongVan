# G7 — frontend Windows ngày 06/10/2026

Frontend đã kiểm chứng trên **Windows với Node22.23.3**: locked install, Prisma generate, Next typegen, typecheck, lint, **223/223 tests** và **full build** đều qua. Không failure/skip/cancelled. [Evidence](WINDOWS-FRONTEND-VERIFICATION-20261006.json).

Bản nguồn 1.552 file lấy bằng Git archive commit `cfde43452ef995d3614ef5e02593faa02b337626`, chạy trong thư mục QA riêng. Toàn bộ đầu vào tracked trong bản build giữ nguyên bytes; 1.159 file frontend/Prisma/công cụ chạy (gồm README frontend) đối chiếu với repo hiện hành vẫn khớp. Không thay app source, dependency hoặc lockfile để qua kiểm tra này.

Node22 portable lấy từ Node.js chính thức, checksum archive `2b0ff57b049cda1bbcea2240eec20467018713c1efe1f7360c2681859b90ed71` khớp [SHASUMS256](https://nodejs.org/dist/v22.23.3/SHASUMS256.txt), runtime version đã kiểm. Không thay Node hệ thống. QA dùng whitelist biến Windows, hai npm config rỗng riêng, cache/temp mới và tarball Iconify chính thức đã kiểm hash; TLS bình thường. Không lấy cấu hình/credential thật, mở web server, bật worker hoặc gửi mail/task.

Bootstrap v1 đã terminal Failed trước web profile vì npm từ chối cùng một config file nạp cả user/global. Đã tách hai file rỗng và chạy v2; giữ log cũ, dùng cùng nguồn đã kiểm. Đây là sửa môi trường QA, không đổi code sản phẩm.

Unified exec session 81113 đã trả exit0/Finished; không còn lượt Windows đang chờ. Raw reports ở `.artifacts/qa/windows-frontend-20261006`, report cuối `status-v2.json` và `source/.artifacts/qa/windows-web-v2/`. File handover đang chạy trước đây là lịch sử sau mốc này.

G7 vẫn Partial/prepared, G8 chưa UAT/signoff. Windows build không chứng minh browser/gateway/authority/customer migration/SQL/SMTP/TMS hoặc đóng security/license gate. EAP thuộc người khác, OCR hoãn. Tiếp tục các kiểm chứng QA còn có thể làm với dữ liệu giả lập; không tự tạo hoặc dùng credential thật.

## Việc QA tiếp theo đã phát hiện

Các fixture SQL không còn dùng Admin để gọi legacy create; chưa thấy fixture cần đổi vai trò. Tuy nhiên `SyntheticLoadSqlTests.FindRoot` còn tìm `scripts/qa/run-synthetic-load.ps1`, trong khi bản repo cộng tác không chứa runner đó. Các thông báo SQL tests cũng trỏ runner PowerShell cũ. Cần chuẩn bị entrypoint SQL QA cho layout hiện hành, xác minh guard chỉ chạy database/container được sở hữu, rồi chạy trên SQL Server QA riêng. Bằng chứng load/restore lịch sử không tự chứng minh entrypoint hiện hành.
