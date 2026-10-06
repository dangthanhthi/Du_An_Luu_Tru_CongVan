# Công cụ kiểm tra

Entry point công khai: `python tools/run-checks.py --profile qa|backend|web|audit --output .artifacts/qa/<tên-mới>`.

- `backend` chạy trực tiếp trên `backend/`, kiểm compile các migration/workflow đã liên kết bằng MSBuild và chạy non-SQL tests.
- `web` chạy trực tiếp trên `frontend/`, locked install, Prisma/Next types, typecheck/lint/tests/full build.
- `audit` kiểm npm và các core NuGet dependencies. Các finding high còn mở phải làm gate fail, không bị bỏ qua để có dấu xanh.
- `qa` dùng `create-check-view.py` tạo source view tạm dưới `.artifacts/qa/`, ánh xạ layout mới về layout các QA tools hiện có. Nó ghi manifest hash nguồn/hash view và ba nhóm adapter đường dẫn (package/schema/MSBuild) được mô tả trong report. Không tạo bản canonical code thứ hai, không thay namespace/API, không copy credential/cache/binary runtime và không sửa nguồn chính.

`tools/qa/` giữ các công cụ QA/runbook đã có: preflight/migration, restore, image/config/startup, CI, dependency/license inventories và bàn giao. Chúng dùng đường dẫn của layout cũ; dùng entrypoint hoặc tạo view rồi chạy công cụ trong view. Không chạy trực tiếp một tool cũ trên layout mới và giả rằng kiểm tra thành công.

Output phải mới để giữ report cũ. Log/cache/DB/test runtime ở `.artifacts/`, bị Git ignore. Khi thay nguồn đã ánh xạ, chạy `python tools/update-source-manifest.py`; khi thêm/xóa/di chuyển file phải cập nhật cả mapping và test.

Các kiểm tra dùng dữ liệu giả lập/QA không thay thế gateway/authority/scanner/customer DB/UAT thật. Không bật worker, gửi email/task thật, apply migration lên DB khách hàng hoặc deploy từ các lệnh check mặc định.

Web profile và `npm run build` dùng cùng `frontend/scripts/generate-prisma.cjs`. Việc stage schema là artifact build tạm; không thêm bản schema chính thứ hai hoặc thay package-lock/dependency. Compatibility view chuyển lệnh build về generate tại schema đã ánh xạ trong view.
