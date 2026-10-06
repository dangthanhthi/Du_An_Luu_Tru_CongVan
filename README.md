# DAS — Quản lý và lưu trữ công văn

Bản cộng tác của dự án DAS, gom từ nguồn đang phát triển ngày 06/10/2026. Đây là bản **đang hoàn thiện**, dùng để đồng nghiệp đọc mã, chạy kiểm tra và cùng phát triển. G7 còn các gate vận hành; G8 chưa nghiệm thu. EAP do người khác phụ trách, OCR hoãn, SMTP/TMS chưa bật giao tiếp thật.

Mốc nhắc hạn chính thức là **quá 7 ngày**, lịch thứ Hai 08:00 Việt Nam. Xem [checkpoint hoàn thiện](docs/COMPLETION-CHECKPOINT-20261006.md) để tiếp tục đúng bản nguồn và phạm vi.

Đã kiểm [CI hosted trên runtime `abb54a9`](docs/HOSTED-CI-CHECKPOINT-20261006.md): backend629 mỗi OS, frontend223, SQL77 và layout/QA qua. Full workflow vẫn failure do dependency audit6high; G7/G8 chưa được nghiệm thu.

## Cấu trúc

| Thư mục | Nội dung chính | Bắt đầu đọc |
|---|---|---|
| `frontend/` | Next.js, giao diện DAS, API client, frontend tests | [Hướng dẫn frontend](frontend/README.md) |
| `backend/` | Gateway, 6 service .NET, DTO/controller, DbContext/entity, backend tests | [Hướng dẫn backend](backend/README.md) |
| `database/` | EF migrations chia theo service và schema Prisma | [Hướng dẫn database](database/README.md) |
| `workflows/` | Mã luồng đăng ký/sửa/phân phối/hủy/khôi phục, PDF, task, báo cáo, nhắc hạn và notification | [Bản đồ workflow](workflows/README.md) |
| `tools/` | Công cụ kiểm tra và QA | [Cách chạy kiểm tra](tools/README.md) |
| `tests/` | Kiểm tra cấu trúc repository và QA tools; test ứng dụng nằm trong frontend/backend | [Quy tắc cộng tác](CONTRIBUTING.md) |
| `docs/` | Nghiệp vụ, kiến trúc, tiến độ, việc còn lại, ánh xạ nguồn | [Trạng thái hiện tại](docs/COLLABORATION-STATUS.md), [kiến trúc](docs/ARCHITECTURE.md) |
| `.github/workflows/` | CI; giữ đúng vị trí GitHub yêu cầu | [CI](.github/workflows/core-ci.yml) |

Database migration và workflow C# chỉ có **một bản nguồn chính**. `backend/Directory.Build.props` liên kết chúng vào service sở hữu khi compile; namespace, assembly và hợp đồng HTTP được giữ nguyên. DbContext/entity ở service sở hữu để không dùng chung DB giữa service.

## Yêu cầu và kiểm tra

Cần Node.js22, Python3.12 và .NET SDK10. Chạy từ root repository:

```sh
python -m unittest discover -s tests/layout -v
python tools/run-checks.py --profile qa --output .artifacts/qa/qa
python tools/run-checks.py --profile backend --output .artifacts/qa/backend
python tools/run-checks.py --profile web --output .artifacts/qa/web
python tools/qa/run-isolated-sql.py --profile all --output .artifacts/qa/sql
```

Mỗi output phải là thư mục mới. Backend check chạy non-SQL tests; [SQL QA](docs/SQL-QA.md) dùng Docker SQL riêng với image cố định và dữ liệu giả lập, không nhận database có sẵn. Live gateway/authority/SMTP/TMS/UAT cần môi trường riêng. Web check cài dependency từ lockfile, sinh Prisma/Next types, typecheck, lint, test và build. Không bật worker hoặc gửi dữ liệu thật.

Để làm giao diện:

```sh
cd frontend
npm ci --ignore-scripts --no-audit --no-fund
npm run generate:prisma
# Tạo .env.local từ .env.example và đặt secret riêng trên máy.
npm run dev
```

Frontend mặc định dùng gateway `http://localhost:8080`. Chưa có tài khoản demo hoặc giả quyền; backend cần cấu hình và tài khoản hợp lệ. Hướng dẫn cấu hình ở [backend/README.md](backend/README.md).

Đọc [CONTRIBUTING](CONTRIBUTING.md) trước khi sửa. [Trạng thái và giới hạn kiểm chứng](docs/COLLABORATION-STATUS.md) là nguồn tiến độ của bản này; số test trên nguồn trước khi sắp xếp không tự chứng minh cấu trúc mới.
