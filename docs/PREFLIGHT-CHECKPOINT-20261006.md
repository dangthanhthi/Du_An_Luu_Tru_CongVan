# G7 — checkpoint preflight CLI ngày 06/10/2026

Nguồn: `DAS-Collaboration`, nhánh `codex/das-completion-20261006`, baseline trước lượt này `bac9c4b8c511b9460c8fd2bef635041018035bd5`. Tiếp tục mục tiêu DAS hiện hành; EAP thuộc người khác, OCR hoãn; không bật transport hoặc chuyển dữ liệu thật.

## Lỗi đã xác nhận và thay đổi

CLI cũ dùng `write_text` lên output đã tồn tại, nên output hardlink có thể ghi đè JSON nguồn hoặc PDF, trái với cam kết chỉ đọc. Công cụ cũng parse và hash bằng hai lần đọc file khác nhau; JSON lỗi không có report được cấu trúc.

Đã sửa:

- Parse và hash cùng một snapshot bytes UTF-8, giữ BOM trong hash; không đọc nguồn lần hai.
- JSON lỗi, duplicate key, NaN/Infinity hoặc encoding lỗi có report `INVALID_EXPORT_JSON`; đọc file lỗi có `EXPORT_READ_FAILED`, hash null. Không lộ raw nội dung input trong report/stderr.
- Output phải mới và được tạo bằng chế độ exclusive; không ghi đè report, source hoặc hardlink. Reject parent traversal, symlink/junction, Windows ADS/device aliases, namespace, trailing dot/space và tên không portable.
- Giữ số công văn lịch sử, dữ liệu nguồn và PDF; không thêm lệnh import/migrate, không gọi database/scanner/SMTP/TMS.

Review độc lập phát hiện hai edge `..` và ADS. Đã tái hiện, thêm regression và sửa; review cuối không còn lỗi actionable trong phạm vi. Đường dẫn Windows/POSIX bình thường, UNC, thư mục ẩn và tên tiếng Việt vẫn được nhận. Không tuyên bố static path checks thay cơ chế cô lập filesystem khi tiến trình khác đồng thời thay cấu trúc thư mục.

## Bằng chứng

- RED đầu: 9 failures/22 tests, gồm mutation thực tế của JSON/PDF qua hardlink.
- Traversal RED: 1/23. Windows aliases RED: 7 subtest failures/25. Namespace RED: 1/26.
- GREEN cuối: **26/26 trên Windows và 26/26 Linux**, không skip. Linux chạy nonroot, read-only source, network none, không credential/socket/port; source integrity và cleanup qua.
- Toàn bộ Python QA compatibility view: **79/79**. Layout: **10/10**.
- Đối chiếu nguồn runtime đã kiểm trước: **340 backend/workflow/database/build inputs và 1.158 frontend/Prisma/build inputs vẫn khớp byte**. Không chạy lặp backend/frontend khi các đầu vào đó không đổi; kết quả 629/223 và build trước vẫn có đúng phạm vi nguồn.
- [Evidence](PREFLIGHT-VERIFICATION-20261006.json), [hướng dẫn chạy](MIGRATION-PREFLIGHT.md). Raw logs/source fixture chỉ ở `.artifacts/qa/preflight-cli-20261006`, ngoài Git. Các log RED cũ được giữ nguyên.

## Trạng thái và bước tiếp

G7 vẫn Partial/prepared. Customer export/mapping/đối soát thật, scanner/authority/storage, SMTP/TMS, RPO/RTO/SLA, hosted CI/registry/signing/scanning, security/license và UAT/pilot chưa nghiệm thu. G8 vẫn chờ mentor chạy/signoff. Không tự mở lại EAP hoặc OCR.

Gate QA frontend Windows đã được kiểm tiếp bằng runtime Node22.23.3 chính thức: 223/223 tests, typecheck/lint/full build và source integrity qua. Xem [checkpoint Windows](WINDOWS-FRONTEND-CHECKPOINT-20261006.md). Giữ Node24 hệ thống nguyên trạng; không dùng kết quả Node24 để gắn nhãn Node22 hoặc yêu cầu credential thật cho QA cục bộ.
