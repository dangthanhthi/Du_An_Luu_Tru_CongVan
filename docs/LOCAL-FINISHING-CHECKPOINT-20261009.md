# Checkpoint chốt tài liệu, gói nhánh và Docker context — 09/10/2026

> Cập nhật sau lượt này: Docker đã build/chạy thật và chốt kiểm local. Đọc [checkpoint Docker mới](DOCKER-EXECUTION-CHECKPOINT-20261009.md):6 backend +1 frontend image đạt,41 backend và25 frontend HTTP checks,15 layout/13 recipe tests. Docker/WSL đã dừng, cấu hình WSL tạm đã gỡ. Các mô tả “chưa chạy Docker” phía dưới là lịch sử của lượt chuẩn bị context.

Lượt này xử lý các phần còn làm được với thông tin hiện có. Không thay code nghiệp vụ, không ghép backend chính, không commit/push hoặc cập nhật database. Nguồn J20–J23 và kiểm chứng nghiệp vụ vẫn theo [checkpoint trước](J21-J23-COMPLETION-CHECKPOINT-20261009.md).

## Đã hoàn thành

1. Cập nhật dependency, namespace/DTO và phương án ghép theo J21–J23 đã triển khai. Bổ sung DI `V2LifecycleHistory` ở Document và `PartnerAuditQuery` ở Partner; tách history Int64 string/UTCZ khỏi mutation version number hiện hành. Sửa các dòng cũ còn ghi J23 chưa triển khai.
2. Bản đồ source JSON/Markdown đã cập nhật **120 file**, đối chiếu byte hash với nguồn local. Bốn project MSBuild: Document98, Partner13, Files27, Notification16 Compile items, không trùng đường dẫn; các source trong map đều có nơi compile đúng. Đây là kiểm compilation list/DI, không phải chạy lại full build hoặc production.
3. Chuẩn bị riêng **60 file** cho bốn nhánh trong `.artifacts/local-finishing-20261009/packages/`. Chỉ lấy source/module documentation đúng chủ đề, không có tests/checkpoint/bàn giao, migrations mới, config/secret, DB/PDF, cache/output hoặc frontend/workflow ngoài phạm vi. Code khác chỉ vì format đã giữ byte từ nhánh hiện có để tránh thay đổi thừa.

| Nhánh | File đã chuẩn bị | Thay đổi thực so với nhánh local official hiện có |
|---|---:|---:|
| database | 35 | 0 |
| numbering | 3 | 1 README |
| shared-contracts | 16 | 3: V2HistoryContracts, README và namespace/DTO |
| architecture | 6 | 4: dependency, integration plan, file map MD/JSON |

Các thay đổi này **chưa stage/commit/push**. Official checkout sạch và refs main/database/numbering/shared-contracts/architecture không đổi so với baseline bắt đầu lượt này. So sánh dựa trên refs local đã có, không tuyên bố vừa fetch/kiểm remote mới. Controllers/query và Partner DTO đang cùng workflow không bị sao chép vào nhánh shared-contracts để tạo class trùng.

## Docker đã kiểm tới đâu

- Docker CLI có, daemon desktop-linux đang dừng. Không khởi động Docker Desktop vì máy chỉ còn khoảng 300–800MB RAM trống trong lượt này và người dùng yêu cầu giảm tác vụ nền.
- Tạo compatibility view chỉ chứa source từ layout canonical, giữ hash/provenance và chuyển các MSBuild-linked workflow/migration về đúng owner trong view. Không sửa source sản phẩm; đây là view QA riêng, không phải bản để push.
- Xuất source context bounded cho 6 host Auth/Document/Files/Notification/Partner/Gateway. Đối chiếu csproj/lockfile/source hashes, J23 history/numbering/migration dependency và PdfProtocol có mặt; loại appsettings/config local, DB, output và các folder dữ liệu.
- Frontend source context có 1147 file, không chứa env/DB/credential/backend. Context riêng trong compatibility view.
- **11/11 recipe tests đạt**: 5 core-image và 6 frontend-image. Kiểm exclusion, hash, symlink/overwrite guard, base-reference và disabled worker defaults.
- Docker recipe chuẩn bị dùng official .NET10 tags. Chưa inspect/pin base digest, chưa build image, chưa smoke runtime/app, chưa kiểm native SQLite trong container. **Không có Docker build/runtime PASS trong lượt này.** Không có deployment hoặc production readiness.

Evidence: `.artifacts/local-finishing-20261009/docker-context-verification.json`, các manifest/contexts, hai log test recipe và `final-verification.json`.

## Bước tiếp tục Docker khi đủ RAM

Giữ product source hiện hành, tạo view mới qua `python tools/create-check-view.py --output .artifacts/qa/<tên-mới>` với `PYTHONIOENCODING=utf-8`. Docker image runner legacy phải chạy **từ view**, dùng `scripts/qa/build-core-images.py --directory .artifacts/qa/<tên-output-mới>`; không chạy trực tiếp từ root canonical với đường dẫn backend cũ. Runner sẽ pull official bases, inspect immutable digests, build từng host tuần tự, kiểm non-root/worker defaults/runtime assets và lưu log. Đây vẫn chưa thay nghiệm thu khởi động app với authority/schema thật.

Frontend image runner còn yêu cầu official Node digest và tooling image đã có; không bịa digest hoặc dùng context frontend chỉ có source như thể đã chứa output standalone. Dùng pipeline đã có trong `tools/qa/build-frontend-image.py` qua compatibility view và kiểm inputs theo source mới. Không tự bật SMTP/reminder/intake/EAP/OCR/TMS trong smoke.

## Tài nguyên và trạng thái lúc chốt

- Các cổng3211/8080/5001–5005/5007 đã đóng khi kiểm lượt này. Không khởi động lại DAS preview, Docker hoặc build/test daemon nặng.
- Đã dừng5 tiến trình NVIDIA Overlay để giảm tác vụ nền, nhưng supervisor NVIDIA tự khởi động lại chúng. Không tắt nvcontainer/driver để tránh ảnh hưởng game; đây không phải xác nhận overlay đã tắt vĩnh viễn. Giữ Roblox, Zalo, Brave, ứng dụng ChatGPT/Codex, Windows/security services và SQL Server có sẵn. Không đóng ứng dụng người dùng hoặc kill PID lịch sử.
- Ghi nhận RAM trống biến động, không hứa một lượng RAM giải phóng cố định hoặc quy toàn bộ RAM cho DAS. Lượt kiểm cuối chỉ dùng script nhẹ/MSBuild evaluation, không full build/test suite hay SQL server mới.
- Source/checkpoint/package mới nằm local. Bản preview được ghi là đang chạy trong checkpoint trước nay là trạng thái lịch sử; muốn xem web cần start lại với launcher private và kiểm owner/ports.

## Còn lại của phạm vi này

Build/chạy Docker thật còn pending vì tài nguyên/daemon local, **không phải thiếu quy định nghiệp vụ**. Việc xuất bản60 file cần yêu cầu Git riêng và chỉ push đúng các file thay đổi cần thiết. Những gate TMS/SMTP/authority/customer/migration/UAT/deploy/license/security còn lại không được coi là đã hoàn thành từ việc chốt tài liệu/gói nhánh này.
