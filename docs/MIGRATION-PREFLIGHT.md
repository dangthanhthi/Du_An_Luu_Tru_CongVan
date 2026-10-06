# Preflight export/PDF trước khi chuyển dữ liệu

Đây là bước kiểm tra chỉ đọc cho JSON đã được chuẩn hóa, không kết nối database, nhập công văn, cấp lại số hoặc thay scanner. Extract từ database cũ và mapping khách hàng vẫn cần được bàn giao, duyệt và đối soát riêng.

## Chạy từ repository chính

Thay `INPUT/export.json` và `INPUT/pdf` bằng nguồn được phép kiểm tra. Output phải là một tên file mới, bên ngoài cây PDF nguồn:

```sh
python tools/qa/audit-migration-export.py INPUT/export.json --pdf-root INPUT/pdf --output .artifacts/qa/preflight-001/report.json
```

Nếu chưa có PDF bytes, có thể bỏ `--pdf-root`; metadata vẫn được kiểm tra nhưng warning `PDF_BYTES_NOT_VERIFIED` không chứng minh file tồn tại hoặc scanner đã chấp nhận.

Mỗi lượt dùng output mới. Không ghi đè report cũ, kể cả khi output là alias/hardlink. Đường dẫn output không được chứa `..`, symlink/junction, ADS/device namespace, tên device Windows hoặc thành phần có dấu chấm/khoảng trắng ở cuối. Đường dẫn Windows/POSIX thông thường, tên tiếng Việt và thư mục `.artifacts` được hỗ trợ.

## Đọc kết quả

| Exit code | Ý nghĩa |
|---|---|
| `0` | Kiểm tra staging export qua; chưa phải nghiệm thu migration/live |
| `1` | Report chứa lỗi dữ liệu, JSON hoặc đọc input; giữ nguyên nguồn và sửa đầu vào theo quy trình đối soát |
| `2` | CLI/output không hợp lệ hoặc không ghi được report mới; không dùng report thiếu/không hoàn chỉnh để chấp nhận dữ liệu |

`sourceSha256` được tính từ đúng byte snapshot đã parse, gồm UTF-8 BOM nếu có. JSON lỗi encoding/cú pháp, duplicate key hoặc NaN/Infinity trả `INVALID_EXPORT_JSON`; không đưa raw input vào stderr/report. Không đọc được file trả `EXPORT_READ_FAILED` và `sourceSha256=null`, không tạo hash giả.

`mutated=false` và `liveAcceptance=false` giữ nguyên: công cụ không sửa export/PDF, không đối soát database thật và không chứng minh authority/scanner. Signature `%PDF-`, size/hash/path chỉ là preflight; quyền đọc, scanner, claim hiện hành và mapping vẫn cần kiểm tra riêng. Report có highwater để đối soát counter; số công văn lịch sử không bị tạo lại.

Không đưa input khách hàng, PDF, credential hoặc raw report vào Git. Chỉ chạy trên dữ liệu được cấp và thư mục output được kiểm soát; path checks không thay cơ chế cô lập filesystem khi có tiến trình khác đồng thời sửa cấu trúc thư mục.
