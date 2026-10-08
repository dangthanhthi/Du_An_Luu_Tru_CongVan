# Kiểm thử SQL riêng cho DAS

Runner canonical: `tools/qa/run-isolated-sql.py`. Chạy từ root repository với Python 3.12, .NET SDK 10 và Docker Desktop Linux/local Docker Engine. Runner yêu cầu image SQL Server 2022 Developer chính thức đã có trên máy, cố định digest:

```sh
docker pull mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090
python tools/qa/run-isolated-sql.py --profile all --output .artifacts/qa/sql-run-01
```

Runner không tự pull hoặc dùng tag thay đổi. SQL Developer chỉ dùng cho kiểm thử. Có thể chỉ định đường dẫn executable bằng `--dotnet` / `--docker`; không có tham số connection string, database, container/network có sẵn hay credential thật. Docker context hiện hành phải là socket local `npipe://` hoặc `unix://`; runner bỏ `DOCKER_HOST` và các override environment kế thừa.

| Profile | Phạm vi |
|---|---|
| `core` | SQL tests của Document, Files, Partner, Auth, Notification, EmailWorker; loại fixture load và restore Email để không có skip |
| `load` | 300 registrations, 300 exact replays, race/conflict/counter/idempotency với dữ liệu giả lập |
| `restore` | Backup CHECKSUM, VERIFYONLY, restore 5 SQL store + PDF giả lập; sau đó diễn tập độc lập Email Worker settings/logs/migration history và FK; tất cả worker tắt |
| `all` | Cả ba nhóm, tuần tự trên một instance riêng |

`--profile core --service EmailWorkerService` giới hạn kiểm tra một service; tên service được ghi vào `selectedService` trong summary. Không dùng kết quả focused để tuyên bố toàn bộ core đã qua. `--service` không được kết hợp load/restore/all. Build/obj của các SQL test nằm trong output riêng (`--artifacts-path`), không ghi đè assembly đang chạy local.

Mỗi lượt tạo container/network với token ngẫu nhiên và label ownership. SQL chạy nonroot `mssql`, 3 GiB/2 CPU, `no-new-privileges`, drop capability trừ `NET_BIND_SERVICE` mà executable của image yêu cầu. Không mount host/socket hay dùng volume có sẵn. Bridge riêng chỉ publish `127.0.0.1` tới cổng được Docker cấp. Bridge này **không tắt outbound network**; fixture chỉ kết nối instance SQL vừa tạo, không có credential thật hoặc host worker. `--internal` đã thử nhưng không cấp port binding cho lượt Windows này; runner không fallback mở rộng mạng khi chạy.

Mật khẩu QA sinh trong bộ nhớ và truyền bằng environment; không nằm trong command line, source, summary hoặc log/TRX đã lưu. Environment tiến trình test được whitelist, các worker/SMTP/reminder/intake tắt. NuGet restore dùng lockfile và public config/cache riêng trong output. Test settings giảm parallelism giữa collection; bài kiểm concurrency vẫn chạy các tác vụ song song bên trong fixture.

Output phải là thư mục mới dưới `.artifacts/qa`, không link/junction/traversal. Runner lưu summary, source hash manifest, redacted console logs, TRX, SQL server log; profile restore giữ backup/PDF **giả lập** ngoài Git. Bundle gồm `backups/*.bak`, `storage/*.pdf`, manifest v2 giữ cutId từ fixture và `restore/integrity.json` do verifier chỉ đọc xác nhận. Không đưa cache, `.bak`, PDF hay raw logs lên Git.

Email Worker có `restore-email/roundtrip.json`, `restore-email/emailworker.bak` và `restore-email/integrity.json`; summary ghi `emailRestore`. Checksum backup được đối chiếu với file nguồn trong container, dữ liệu/migration trước và sau phải có hash bằng nhau, worker flags phải đều false. Báo cáo chỉ chứa metadata/hashes, không chứa cấu hình mailbox hoặc password. Cut này độc lập với bundle `core-five-stores`; không gọi hai kết quả là backup đồng bộ sáu store.

Trên máy RAM 8 GB, chạy một lượt `all` để các suite dùng chung cache/build của lượt đó, không mở nhiều runner đồng thời. Tạm dừng preview không cần cho SQL và giữ nguyên bài concurrency bên trong fixture. Sau khi runner kết thúc và xác nhận cleanup, giữ summary/source manifests/log/TRX/backup/PDF; có thể dọn riêng `build`, `nuget-packages`, `dotnet-home` của lượt đó sau khi kiểm tra đường dẫn và chắc chắn không có tiến trình đang dùng. Không xóa cả thư mục bằng chứng hay Docker volumes/images khác.

CI `.github/workflows/core-ci.yml` có job `sql-qa` chạy khi chủ động `workflow_dispatch`; PR thông thường vẫn chạy core checks. Image và action giữ digest/commit cố định, token chỉ `contents:read`, checkout không persist credential. Job chỉ upload report/log/TRX đã redacted trong 7 ngày, không upload NuGet cache/backup/PDF. SQL hosted đã qua trên commit `abb54a9`; full workflow vẫn failure do dependency audit. Xem [checkpoint hosted](HOSTED-CI-CHECKPOINT-20261006.md) để giữ đúng scope và source SHA.

Exit 0 chỉ khi tất cả step và TRX có test thực sự chạy/pass, không fail/skip, nguồn giữ nguyên và cleanup xác nhận. Runner chỉ xóa container/network khớp name/label/ID của lượt hiện tại. Lỗi daemon/inspect không được coi là đã dọn; summary ghi `cleanupConfirmed=false` và exit lỗi. Khi cần xử lý sự cố, kiểm token trong summary và đối chiếu label trước mọi thao tác; không xóa theo tên chung `sql` hoặc tất cả container.

Kiểm thử này không apply migration lên khách hàng, không nhập export thật, không nghiệm thu browser/gateway/authority/TMS/SMTP/EAP/OCR, RPO/RTO, HTTP SLA hoặc UAT. G7 vẫn còn các gate thực tế; G8 cần môi trường và mentor ký.
