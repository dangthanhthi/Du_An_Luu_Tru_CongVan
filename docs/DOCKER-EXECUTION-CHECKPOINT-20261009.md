# Kiểm chứng Docker local — 09/10/2026

> Cập nhật sau kiểm thử: các image QA DAS và cache build đã được xóa theo yêu cầu giải phóng dung lượng. Kết quả kiểm thử bên dưới được giữ; muốn chạy lại Docker cần build lại image. Docker/WSL hiện dừng.

Tiếp tục phần Docker còn pending trong [checkpoint chốt local](LOCAL-FINISHING-CHECKPOINT-20261009.md). Nguồn phát triển vẫn là DAS-Collaboration. Không commit/push, không ghép/sửa checkout backend chính thức, không dùng dữ liệu khách hàng hoặc bật EAP/OCR/TMS/SMTP thật.

## Kết quả đã kiểm

| Hạng mục | Kết quả | Phạm vi |
|---|---|---|
| Backend image | 6/6 build và runtime/package smoke đạt | Auth, Document, Files, Notification, Partner, Gateway; .NET10 Linux amd64 |
| Startup backend | 6/6 host, 41/41 kiểm tra đạt | Development, DB mới riêng, JWT reader tổng hợp, health/401/CORS/503 authority unavailable; Gateway proxy qua Partner |
| SQLite native | 5/5 store đã khởi tạo và đọc trong container | Auth8, Document23, Files3, Notification4, Partner2 bảng; snapshot integrity/foreign-key check đạt |
| Layout | 15/15 đạt | Hash nguồn, LF, source ownership, compatibility view và chặn đường dẫn không an toàn |
| Recipe Docker | 13/13 đạt | 5 core-image và 8 frontend-image tests; không thay cho việc chạy app |
| Frontend image/HTTP | 1/1 image, 25/25 HTTP đạt | Next16.3.8/Node22 Linux, production build gồm TypeScript, standalone nonroot UID1000 |
| Nguồn và Git | 1357 file sản phẩm đối chiếu đạt | Hash canonical/view/image inputs không drift; official checkout sạch và5 branch refs không đổi |

Các image dùng official base digest đã inspect, UID1654 cho backend, workers/seed/init mặc định tắt. Runtime QA chủ động chọn Development và bật khởi tạo **database mới trên anonymous volume riêng**; không nới cấu hình Production. Giới hạn mỗi backend512MiB/1CPU, root filesystem chỉ đọc, cap-drop ALL, no-new-privileges, HTTP chỉ publish loopback. Chạy tuần tự; chỉ giữ cặp Gateway+Partner trong bước kiểm proxy. Không kiểm đầy đủ liên thông mọi service hoặc SQL Server Production trong topology này.

Auth/Document vẫn trả503 khi reader chưa có authority/directory thật. Không dùng token tổng hợp để cấp quyền giả cho nghiệp vụ hoặc tuyên bố EAP đã tích hợp. Năm DB được sao chép khi container tạm pause, gồm WAL/SHM nếu có; nguồn native SQLite đã thực thi SQL và snapshot được kiểm read-only. Container, volume và network của lượt startup đã xóa, cleanupPassed=true.

## Lỗi đã xử lý trong lượt này

1. Build Document ban đầu thiếu `V2KindDetailsView`: provenance note của file `V2ReadContracts.cs` bị hiểu thành đường dẫn trong compatibility view. Sửa `tools/create-check-view.py` để file local mới về đúng source scope; kiểm unsafe source trước fallback. Regression RED→GREEN cho DTO và parent/drive/UNC paths; full layout15/15. Không sửa DTO hoặc nghiệp vụ.
2. Chuẩn hóa LF đúng quy định `.gitattributes` cho11 file frontend có CRLF; chỉ đổi byte xuống dòng, không đổi JSON dependency hoặc TypeScript logic. Cập nhật hash manifest, giữ thông tin nguồn gốc.
3. `docker cp` không thấy database trong tmpfs dù app đã truy vấn được. QA chuyển sang anonymous volume mới tại `/app/storage` với owner1654. Chỉ xóa resource được xác minh owner; không prune volume/image của dự án khác.
4. Gateway chạy đơn lẻ không có downstream trả502. Lượt kiểm cuối dùng cặp Gateway+Partner và override endpoint riêng bằng environment; đã xác nhận401 anonymous và200 reader qua proxy. Không đổi route config sản phẩm.
5. Frontend lần đầu: webpack compile đạt, TypeScript worker hết heap1280MiB. Đã dừng đúng container build có label task; exit137 do dừng, `oomKilled=false`, cleanup đạt. Chạy lại với heap2048MiB, giữ RAM vật lý1792MiB, tổng RAM+swap3GiB/1CPU và tắt core dumps; không bỏ bước TypeScript.
6. Probe trang template cũ yêu cầu noindex cho HTTP200 chứa404. Giao diện hiện có session gate khi SSR nên HTML chỉ hiện “Đang kiểm tra phiên…” và RSC404 digest, không có noindex. Sửa helper QA để nhận đúng tổ hợp này; session shell hoặc404 marker đứng một mình vẫn không đạt. Hai regression RED→GREEN; frontend recipe8/8 và HTTP cuối25/25. Không thay giao diện hoặc bỏ gate.

Frontend build lần cuối `compileExitCode=0`, `oomKilled=false`, `sourceChanged=[]`, cleanupPassed=true. TypeScript hoàn tất3,6 phút trong giới hạn RAM/swap; webpack104 giây. HTTP kiểm login SSR, static/public asset, root redirect, anonymous session, template login410, email/OCR503,10 API template410,5 trang template404 boundary và package không có credential/DB. Chưa phải đăng nhập thật, UI nghiệp vụ Docker với customer DB hay nghiệm thu Production; các browser/backend nghiệp vụ trước đó ở checkpoint J21–J23.

## Tài nguyên và ứng dụng

Người dùng cho phép đóng Roblox và các ứng dụng đang dùng trong lượt này. Đã dừng RobloxPlayer/CrashHandler, Zalo và wallpaper64. Giữ ứng dụng Codex/ChatGPT và dịch vụ Windows/security cần thiết; không ép đóng tài liệu chưa lưu. NVIDIA overlay có supervisor tự khởi động lại, không coi việc kill tạm là đã tắt vĩnh viễn.

Docker Desktop đã khởi động để kiểm rồi **đã dừng**. Không có `.wslconfig` trước lượt này, nên tạo giới hạn tạm2GB RAM/2CPU/2GB swap, autoMemoryReclaim gradual. Đã xác minh Docker API không còn, Docker backend/UI/WSL VM không chạy, rồi gỡ đúng file cấu hình tạm sau khi hash khớp. Không ghi đè thay đổi người dùng. Các container cũ BigData không chạy lúc kiểm, không xóa hoặc thay restart policy của chúng.

Lúc chốt: RAM trống khoảng1070MiB, ổC còn21,8GiB; đây là snapshot biến động. Roblox/hình nền vẫn dừng. Zalo đã mở lại trong lượt làm và được giữ khi bước nặng đã xong. Các cổng DAS3211/8080/5001–5005/5007 không lắng nghe. Container/volume/network của các smoke và container/volume build frontend đã dọn; image đã build giữ local để tái dùng. Không có preview hoặc worker tự bật sau task.

Một lỗi đọc UTF-8 của runner private đã tạo thư mục `C:/Users/MSIIIIII/Desktop/Dá»± Ă¡n taskmanager`, chỉ có3 file log/summary. Runner đã sửa để dùng `Path.cwd()`; lệnh xóa thư mục phát sinh bị xét duyệt tự động chặn với lý do “blocked by policy”, nên thư mục vẫn còn. Không tìm cách vượt cơ chế đó.

## Evidence và tiếp tục

- Backend build: `.artifacts/qa/docker-context-fixed-20261009/.artifacts/qa/docker-images-execution-fixed-20261009/summary.json` và6 build/runtime/source logs.
- Startup cuối: `.artifacts/docker-execution-20261009/sequential-startup-pair/summary.json`, service logs và DB tổng hợp riêng. Hai lần probe trước thất bại được giữ nguyên, không ghi đè.
- Frontend: `.artifacts/qa/docker-frontend-fixed-20261009/.artifacts/qa/frontend-image-execution-retry-20261009/`; lần1280MiB ở `frontend-image-execution-20261009/`.
- HTTP frontend cuối: `.artifacts/qa/docker-frontend-fixed-20261009/.artifacts/qa/frontend-smoke-execution-final-20261009/summary.json`. Hai probe cũ thất bại và HTML diagnostic được giữ riêng.
- Layout/recipe logs, giới hạn WSL, runner private và trạng thái cleanup: `.artifacts/docker-execution-20261009/`.
- Chốt nguồn/hash/Git: `final-verification.json`; chốt Docker/WSL/RAM/ứng dụng: `resource-restoration.json`. Đọc report sau cùng, không lấy tên một log “failed” trước sửa để thay thế kết quả cuối.

Không đưa các evidence, database, runtime output, cấu hình tạm hoặc image lên Git. Bốn gói nhánh chuẩn bị trước giữ nguyên phạm vi. G7 vẫn Partial/prepared; G8 chưa customer UAT/signoff. Các đầu vào thật còn thiếu theo checkpoint J21–J23, không được coi là đã hoàn thành từ kiểm Docker Development.
