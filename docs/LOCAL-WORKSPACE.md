# Cấu trúc làm việc trên máy và GitHub

Nguồn DAS đang làm việc nằm trong thư mục `DAS-Collaboration`. Root của thư mục này chính là root repository GitHub; vì vậy `DAS-Collaboration/frontend` trên máy tương ứng `frontend` trên GitHub.

| Thư mục trong repository | Nội dung |
|---|---|
| `frontend/` | Giao diện Next.js, API client, frontend tests |
| `backend/` | Gateway, service .NET, controller/DTO/entity/DbContext và backend tests |
| `database/` | Migrations theo service và Prisma schema |
| `workflows/` | Luồng nghiệp vụ, tác vụ, báo cáo, nhắc hạn và notification |
| `docs/` | Yêu cầu, kiến trúc, checkpoint và evidence cần cho cộng tác |
| `tests/` | Layout tests và QA tools tests |
| `tools/` | Công cụ kiểm tra, preflight, migration và QA |
| `.github/workflows/` | Cấu hình GitHub Actions |

Sửa code và chạy lệnh Git trong `DAS-Collaboration`. Workflow/migration C# chỉ có một bản nguồn chính, được liên kết vào đúng service bằng `backend/Directory.Build.props`; không sao chép chúng trở lại service.

Ngày 06/10/2026, các bản code cũ ở thư mục cha đã chuyển vào `archive/legacy-sources-20261006`: `DAS-Frontend`, `Intern-DocumentAdministration-BE`, `Intern-DocumentAdministration-FE-Web` và ứng dụng cũ ở root (`src`, `public`, package/config, launcher, dependency/build cache). Giữ nguyên tên tương đối trong archive, Git history của các repository con và các file chưa commit. Archive được đối chiếu hash nguồn trước/sau; không xóa dữ liệu. Archive chỉ ở máy, không đưa lên GitHub và không phải nguồn đang phát triển.

Thư mục `CanChinhSuaDeThuongMaiHoa` vẫn giữ tài liệu mentor, checkpoint riêng và dữ liệu/config được cung cấp. Thư mục `.agents`, công cụ và hồ sơ kiểm thử cũ ở thư mục cha được giữ để không làm hỏng môi trường Codex. Repository Git lịch sử ở thư mục cha không dùng để push bản cộng tác; tránh chạy `git add` từ thư mục cha.

Dependency/build cache, `.env`/credential, DB/PDF khách hàng, bản gốc mentor, screenshot và raw logs chỉ giữ trên máy. Đồng bộ Git chỉ gồm source, lockfile, test và tài liệu cần thiết. Sau khi lấy code, mỗi máy cài dependency theo hướng dẫn [README](../README.md), không chia sẻ cache/config thật.

[Checkpoint hiện tại](CLIENT-SESSION-CHECKPOINT-20261006.md) · [Bằng chứng publication và sắp xếp](CLIENT-SESSION-PUBLICATION-20261006.json)
