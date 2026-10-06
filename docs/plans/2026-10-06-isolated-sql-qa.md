# SQL QA trên layout cộng tác

Mục tiêu: chạy lại SQL migrations, constraints/concurrency, tải giả lập và backup/restore trên nguồn `backend/database/workflows` hiện hành. Không dùng database, export hoặc credential khách hàng; không mở lại EAP/OCR hay chạy worker thật.

Quyết định đã được người dùng ủy quyền: runner Python tạo SQL Server 2022 Docker riêng, image cố định bằng SHA256 đã có trên máy; mật khẩu ngẫu nhiên chỉ tồn tại trong bộ nhớ/environment tiến trình. Chỉ publish TCP loopback, dùng bridge riêng, không mount host/socket. Nếu Docker/network/readiness không đáp ứng thì thất bại và giữ evidence, không tự mở rộng kết nối.

Thử nghiệm v1/v2/v3 đã dừng và dọn: `--internal` không tạo port binding ở Docker Desktop máy này; image có file capability `cap_net_bind_service=ep`, nên `cap-drop ALL` cần thêm `NET_BIND_SERVICE`. Phương án cuối dùng owned bridge thường và loopback, nonroot + no-new-privileges + chỉ capability nêu trên. Không tuyên bố egress bị tắt. Đây là thay đổi thiết kế có ghi nhận, không fallback runtime; không credential thật hay transport trong fixture.

1. Viết kiểm thử RED cho phạm vi tài nguyên, label ownership, environment, output mới, TRX không rỗng/skip và cleanup thất bại.
2. Viết `tools/qa/run-isolated-sql.py`: profiles core/load/restore/all, không nhận connection string hoặc tài nguyên có sẵn; NuGet public config riêng; giới hạn timeout và tắt transport. Chỉ xóa container/network có label run hiện tại, xác nhận đã dọn. Không coi lỗi inspect là tài nguyên đã mất.
3. Sửa root discovery ở hai fixture tải/restore để nhận layout canonical và giữ compatibility view. Cập nhật hash manifest, giữ provenance nhập nguồn.
4. Chạy unit QA rồi SQL thật trên instance Docker vừa tạo. Core chạy 5 project SQL; load 300 registrations + 300 replays; restore 5 store và PDF, sao lưu `.bak` giả lập giữ ngoài Git. Không quy kết kết quả tải nội bộ thành HTTP SLA hoặc backup thật thành RPO/RTO Production.
5. Nối bundle restore vào verifier v2 có sẵn: giữ cutId thật của fixture, 5 backup + PDF, checksum/dữ liệu và transport tắt. Chỉ giữ bundle mới ngoài Git, không ghi đè artifact trước.
6. Chuẩn bị job SQL trong workflow_dispatch CI hiện có, image digest cố định, chỉ upload report/log/TRX đã redacted, không upload backup/cache/PDF. Hosted run vẫn chưa được chứng minh bằng lượt local.
7. Review độc lập diff, sửa lỗi, ghi checkpoint/evidence và commit local. EAP/OCR, SMTP/TMS/authority/customer migration/UAT/security/license còn mở.

Không nghiệm thu khi test fail/skip/không có test, source đổi trong lượt chạy hoặc cleanup chưa xác nhận. Raw logs/TRX/backups nằm `.artifacts/qa`, chỉ tổng hợp và mã/runbook vào Git.
