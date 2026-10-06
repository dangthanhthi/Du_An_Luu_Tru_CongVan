# Cộng tác trong DAS

1. Lấy bản mới nhất từ `main` và tạo nhánh riêng cho thay đổi. Mỗi pull request tập trung một vấn đề, nêu hành vi trước/sau, test đã chạy và phần chưa có đầu vào.
2. Sửa giao diện ở `frontend/`; API/controller/DTO/persistence ở `backend/`; luồng nghiệp vụ C# ở `workflows/business/<service>/`; migration ở `database/migrations/<service>/`.
3. Không sửa số công văn ở browser, không tự cấp capability/role, không suy task assignment thành quyền đọc tài liệu. Tất cả quyền và số do backend kiểm chứng.
4. Khi thêm migration/workflow, đặt đúng service owner trong `backend/Directory.Build.props`. Không đổi namespace hoặc ID migration chỉ vì chuyển folder; không gộp schema các service.
5. Giữ `frontend/package-lock.json` và `packages.lock.json` của .NET; không cập nhật dependency ngầm. Sau khi sửa file có trong manifest, chạy `python tools/update-source-manifest.py`; nếu thêm/xóa/di chuyển file thì cập nhật ánh xạ trong `docs/source-layout-manifest.json` rõ ràng.
6. Giữ file text UTF-8 với xuống dòng LF theo `.gitattributes`, để manifest hash giống nhau trên Windows/Linux. Chạy layout tests và test phù hợp. Nếu đổi build path/schema/reference thì phải kiểm build thực, không chỉ sửa README. QA tools dùng entrypoint `tools/run-checks.py`.
7. Secret/config riêng đặt ở `.env.local` hoặc biến môi trường trên máy. Không commit credential, DB/backup/PDF upload, log, build, dependency cache, ảnh chụp hoặc dữ liệu khách hàng.
8. EAP/OCR tiếp tục hoãn. SMTP/TMS chỉ nối khi có hợp đồng/config được xác nhận. Không tự đánh dấu UAT, risk/license hoặc go-live là đạt.

Trước khi merge: người review xem diff và bằng chứng test. Bản cộng tác chưa phải bản triển khai production; xem [status](docs/COLLABORATION-STATUS.md).
