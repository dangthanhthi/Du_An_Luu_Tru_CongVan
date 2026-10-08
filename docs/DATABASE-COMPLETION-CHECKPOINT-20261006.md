# Checkpoint hoàn thiện database DAS — 06/10/2026

Phần schema và kiểm chứng database nội bộ đã hoàn tất trong phạm vi có thể làm tại local. **Chưa nhập dữ liệu khách hàng, chưa nghiệm thu database Production/G7/G8.** EAP thuộc nhóm khác; OCR tiếp tục hoãn. Xem [bằng chứng tổng hợp](DATABASE-COMPLETION-VERIFICATION-20261006.json).

## Đã sửa và bổ sung

- Sáu store có migration/snapshot và SQL idempotent: Auth, Document, Files, Notification, Partner, Email Worker. Lượt offline cuối xác nhận **6/6 store, 24 migrations**, model khớp snapshot; không kết nối hoặc apply vào database công ty.
- Sửa lỗi SQL thật ở Email Worker: application lưu cấu hình với `Id=1`, nhưng baseline để IDENTITY nên việc lưu/restore cấu hình thất bại. Mapping dùng khóa cố định; migration mới `20261006154722_FixedEmailSettingsKey` chuyển bảng trong transaction và giữ toàn bộ trường cấu hình. Baseline cũ giữ nguyên.
- Kiểm insert/update singleton, chặn `Id=2`, nâng/hạ/nâng migration với cấu hình cũ, giữ dữ liệu khi gặp khóa bất thường. Migration từ chối FK/index/trigger/quyền/CHECK/default tùy chỉnh; regression SQL kiểm chiều nâng và hạ không làm mất dữ liệu, đối tượng tùy chỉnh hoặc lịch sử migration.
- Thêm restore drill Email riêng: backup CHECKSUM/VERIFYONLY/restore thật, kiểm hash toàn bộ bảng và migration history, các trường nullable, trạng thái/correlation của scan/item log, FK/cascade và singleton. Runner đối chiếu SHA-256 backup trong container với bản copy local. Settings chỉ dùng dữ liệu giả lập không kết nối được; worker/IMAP/manual scan/SMTP/reminder tắt.
- Giữ fixture Email EF9 tách khỏi fixture core EF10. Runner `all` chạy tuần tự trên một instance riêng; core loại fixture restore Email để không có bài bị skip. Cập nhật provisioning và [SQL QA](SQL-QA.md).

## Kết quả xác nhận trên nguồn hiện tại

| Kiểm tra | Kết quả |
|---|---:|
| Document SQL core | 46/46 |
| Files SQL core | 6/6 |
| Partner SQL core | 10/10 |
| Auth SQL core | 9/9 |
| Notification SQL core | 5/5 |
| Email Worker SQL core | 8/8 |
| SQL load | 1/1 |
| Restore năm store core + PDF | 1/1 |
| Restore Email Worker riêng | 1/1 |
| **Tổng SQL** | **87/87; 0 fail/skip** |
| Email Worker không SQL | 13/13 |
| Python QA qua entrypoint canonical | 96/96 |
| Layout | 13/13 |
| Offline schema | 6/6; 24 migrations |

Lượt SQL cuối: `.artifacts/qa/sql-database-complete-20261006-02/summary.json`. Source không đổi trong lượt chạy; container/network đúng ownership đã được dọn, `cleanupConfirmed=true`. Nguồn hiện tại tiếp tục khớp hai source manifest SQL/offline schema khi tổng hợp bằng chứng. Schema cuối nằm ở `.artifacts/qa/database-schema-reviewed-20261006/sql/`.

Bài load tạo **300 công văn + 300 exact replays**, ba counter dùng chung theo loại, mỗi counter 100; không hở số/trùng số, graph nguyên tử, conflict không tăng counter. p95 đăng ký khoảng **95,9 ms** là phép đo service→SQL giả lập, không phải HTTP/Production SLA.

Restore core kiểm năm backup cùng PDF và integrity bundle; restore Email kiểm thêm một backup. **Hai cutId độc lập**, không chứng minh backup đồng bộ sáu store/PDF hoặc đáp ứng RPO/RTO thật. Giữ backup/PDF giả lập ngoài Git.

Layout được kiểm trên source archive của HEAD `ec94a448f4e05f66c47b1342701637f88688a129` cộng các thay đổi database. Chỉ cập nhật dòng manifest của Email DbContext đã sửa; không cập nhật hash để hợp thức hóa các thay đổi frontend/Gemini đang làm riêng. Không chạy lại toàn bộ backend không SQL hoặc kiểm UI trong chặng này; mốc backend631 trước đây là bằng chứng lịch sử, không gọi là lượt kiểm mới.

## Lỗi trước đó và review

Lượt `sql-database-complete-20261006-01` được giữ nguyên: core80/load/restore core qua nhưng restore Email thất bại do explicit IDENTITY insert. Lượt cuối87 qua sau khi sửa. Không xóa bằng chứng RED hoặc đổi timeout/thuật toán cấp số để làm test xanh.

Allocation-lock timeout của checkpoint database cũ không tái hiện trong lượt focused Document và hai lượt `all` mới. Chưa xác định nguyên nhân gốc; chưa suy ra hiệu năng khi tải/môi trường Production khác. Sampler chỉ đọc lưu tại `.artifacts/qa/database-final-diagnostics-20261006/sql-waits-reviewed-20261006.jsonl`.

Independent Codex review chỉ đọc, phát hiện CHECK/default tùy chỉnh có thể bị mất khi dựng lại bảng; đã bổ sung guard và SQL regression Up/Down. Gemini MCP `4-30min` nhận background job nhưng kết thúc 503 ở cả hai tài khoản; không có kết quả Gemini được dùng, không gửi job trùng.

## Dung lượng và trạng thái máy

- Dừng Docker Desktop đã mở riêng cho SQL khi xác nhận không còn container chạy; dừng build server dùng cho lượt kiểm thử.
- Xóa **8 thư mục cache QA** đã kiểm đường dẫn/ownership/links/process; giữ summary, source manifest, SQL, log/TRX, backup/PDF và runtime Node. Dung lượng trống đo ngay trước/sau dọn: **19,14 → 26,52 GiB**, giải phóng thực tế **7,38 GiB**; kích thước file ước tính7,31 không được dùng thay số đo này.
- Snapshot sau đó: C trống **26,66 GiB**, RAM trống khoảng **1.623 MiB**, Roblox vẫn chạy. Không dừng Roblox hoặc sửa dữ liệu cá nhân/Docker volume/VHD.
- Preview3211/gateway8080/services5001–5005/5007 đang tạm dừng; không tự mở lại vì nhiệm vụ hiện tại là database và máy8GB RAM. Database SQLite local vẫn được giữ nguyên.
- Bằng chứng dọn: `.artifacts/qa/database-cache-cleanup-20261006/{cache-cleanup-plan.json,cache-cleanup-result.json}`. Chạy kiểm mới cần output mới; build/cache đã dọn sẽ được tạo lại.

## Việc còn chờ và điểm tiếp tục

1. Customer export/schema/PDF, mapping ID/company/phòng/người dùng/counter/reference codes; đối soát preflight và diễn tập migration trên bản sao được phép. Không tự adopt `EnsureCreated` vào migration history.
2. Quyền và cấu hình database/storage/deployment thật; bảo vệ secret Email/backup, retention và phương án backup có phối hợp; chốt và kiểm RPO/RTO/SLA.
3. Authority/session/CSRF/revocation, TMS/SMTP/audience/scanner và UAT/signoff/pilot vẫn có gate riêng. EAP không triển khai cho đến khi người dùng mở lại rõ ràng; OCR hoãn.
4. UI/Gemini đang sửa riêng cần review và kiểm browser/gateway độc lập; không suy từ SQL xanh rằng UI đã hoàn tất.

Nguồn canonical: `DAS-Collaboration`, nhánh main. Thay đổi của chặng này **chưa commit/push**; HEAD nêu trên là baseline, không phải commit chứa migration mới. Không gộp frontend đang sửa hoặc cache/backup/config thật vào commit database. G7 giữ **Partial/prepared**, G8 chưa nghiệm thu.
