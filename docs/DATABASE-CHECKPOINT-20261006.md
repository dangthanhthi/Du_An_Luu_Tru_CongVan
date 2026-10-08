# Checkpoint database DAS — 06/10/2026

> Cập nhật mới hơn: [checkpoint hoàn thiện database](DATABASE-COMPLETION-CHECKPOINT-20261006.md), [bằng chứng](DATABASE-COMPLETION-VERIFICATION-20261006.json). SQL cuối **87/87**, schema **6/6 với 24 migrations**, restore Email đã bổ sung và qua. Nội dung bên dưới giữ làm lịch sử của mốc trước các sửa mới; không dùng phần “SQL core chưa qua/Email chưa restore” để mô tả hiện trạng.

## Phạm vi và kết quả

Đã bổ sung phần schema/provisioning có thể làm tại local. **Chưa hoàn chỉnh database khách hàng hoặc nghiệm thu G7/G8**. EAP thuộc nhóm khác, OCR hoãn; không dùng credential Task Management/NetBird và không bật mailbox, SMTP hoặc worker thật.

- Email Worker có migration baseline, designer và snapshot cho ba bảng cấu hình IMAP, scan log và item log, nằm trong `database/migrations/email-worker-service`. EF Design/tool giữ major 9 phù hợp service, không nâng framework/dependency khác.
- Email SQL kiểm model/snapshot trước kết nối, đòi migration đã áp dụng khi khởi động không initialize; không tự nhận database cũ tạo bằng EnsureCreated vào lịch sử migration. Development SQL initialize dùng Migrate; Development SQLite vẫn dùng EnsureCreated.
- Notification Development SQL initialize dùng Migrate, tránh tạo bảng rồi thất bại vì không có lịch sử migration. Production không tự sửa schema.
- Auth/Partner design-time factory dùng connection inert riêng; không đọc endpoint/runtime secret của người dùng.
- Có command offline kiểm snapshot và xuất SQL idempotent cho sáu store. Command không kết nối database hoặc áp dụng script. Cấu hình runtime không được truyền vào child process.
- SQL core runner bao gồm Email Worker và cho chọn riêng một service. Build QA chuyển sang thư mục artifacts riêng để không ghi đè DLL preview đang chạy.
- Có hướng dẫn provisioning ở [database/PROVISIONING.md](../database/PROVISIONING.md). Cả sáu store dùng `Database:Initialize=false` khi provision ngoài startup; các seed flag phải tắt tương ứng. Không dựa vào `InitializeOnStartup` để tắt initialization.

## Kiểm chứng và giới hạn

Backend không SQL đã qua **631/631** bài kiểm thử và Gateway Release build; Python QA **94/94** qua. Email schema offline/model-drift **2/2** nằm trong backend suite. Schema offline cuối xác nhận **6/6 store**, 23 migrations, snapshot khớp model và SQL idempotent; không kết nối/apply database, source không thay đổi trong lượt chạy. Layout **13/13** qua trên cây Git tách riêng. SQL riêng Email Worker **4/4**, Notification **5/5** qua, source không đổi và cleanup xác nhận ở cả hai lượt. Kết quả và hash bằng chứng nằm trong [DATABASE-VERIFICATION-20261006.json](DATABASE-VERIFICATION-20261006.json).

Lượt SQL core đầy đủ trên nguồn sau sửa startup **không qua**: DocumentService 45/46, bài `Twenty_independent_keys_share_the_counter_and_create_twenty_atomic_graphs` gặp `Cannot acquire document allocation lock; result=-1`. Runner dừng ở service này; không suy ra năm service còn lại đã qua. Nguồn không thay đổi trong lượt chạy, cleanup instance được xác nhận. Lượt RED trước đó còn timeout CREATE DATABASE tại FileService; Notification RED xác nhận lỗi EnsureCreated/migration history trước khi sửa.

Máy 8 GB RAM có lúc còn dưới 0,2 GB trống. Đây là quan sát về môi trường, **chưa đủ kết luận timeout cấp số chỉ do tài nguyên**. Không đổi thuật toán/timeout để làm test xanh. Bài chịu tải vẫn là gate cần điều tra và kiểm lại trên môi trường đủ tài nguyên.

Điểm điều tra tiếp: `DocumentRegistrationWriter.cs:122` lấy application lock theo loại/năm trong transaction Serializable, `LockTimeout=15000`; bài SQL tạo 20 graph với 20 key riêng cùng counter. Chặng này không thay đổi writer/test đó. Cần đo thời gian giữ transaction, lock waits và tài nguyên SQL trên lượt tái hiện trước khi quyết định sửa. Không coi kiểm Email/Notification là kiểm lại allocation lock.

Kiểm layout phải chạy trên cây Git riêng của commit database: frontend đang được sửa song song. Manifest giữ hash của frontend đã xuất bản, không cập nhật để hợp thức hóa code UI chưa review. Báo cáo UI [UI-GEMINI-HANDOFF-20261006.md](UI-GEMINI-HANDOFF-20261006.md) ghi rõ mốc trước lượt sửa đó.

## Việc còn lại và điểm tiếp tục

1. Đọc evidence đúng phạm vi: SQL riêng Email Worker/Notification và schema offline đã qua, không phải toàn bộ SQL core đã qua.
2. Điều tra timeout allocation lock trên SQL và kiểm lại core/load. Không tuyên bố toàn bộ SQL đã qua dựa trên kiểm thử không SQL.
3. Restore drill hiện có phạm vi **năm database và PDF**; Email Worker chưa nằm trong restore roundtrip. Mở rộng restore khi chốt retention/backup scope.
4. Chờ export/PDF/mapping khách hàng, kiểm schema/ID/quyền/scanner/storage/môi trường thật, RPO/RTO/SLA; không nhập hoặc thay đổi dữ liệu khách hàng khi chưa có nguồn.
5. Chờ authority/session/CSRF/revocation, TMS contract/sandbox/hierarchy/IDs, SMTP/audience/directory, Fax, security/dependency/license/registry/signing/scanning và UAT/signoff. G7 giữ **Partial/prepared**, G8 chưa nghiệm thu.
6. Review các diff frontend đang làm riêng; không đưa chúng vào commit database hoặc dùng chứng cứ frontend291 cũ làm chứng cứ cho UI mới.

## Local preview và tài khoản thử

Preview Development dùng sáu SQLite store riêng trong `.artifacts/local-preview/20261006`, không phải database khách hàng. Web `http://127.0.0.1:3211`, gateway8080, services5001/5002/5003/5004/5005/5007. Authority V2 hiện Unavailable nên health200 không chứng minh nghiệp vụ V2 hoạt động.

Sau kiểm SQL, backend canonical đã locked restore/Release build qua (0 lỗi, 5 warnings hiện hữu). Bảy host backend/gateway đã khởi động lại và health200; Next dev3211 báo Ready. Đây là kiểm host Development, không phải nghiệm thu UI đang sửa song song. Docker Desktop được mở cho SQL QA đã dừng sau khi không còn container chạy; không dừng ứng dụng khác của người dùng.

Tài khoản demo: `secretary_user`, `admin_user`, `employee_user`; mật khẩu `password`. Admin chỉ quản trị master data, không tự có quyền mọi công văn. Không dùng demo seed trong production. Script/process/runtime logs và secret local ở artifacts, không đưa lên Git.

## Review và publication

Independent Codex review đã đọc diff; lỗi hướng dẫn flag initialize đã được sửa. Reviewer không chạy SQL/test. Gemini MCP báo `server_version=4-30min`; một background review thất bại do cả hai tài khoản 503, không có kết quả Gemini được dùng. Không gửi lại job trùng.

Runtime **`d34ec11048c55bbe75849e3c0f8e0e9c840b1d6e`** đã push main; remote ref được đọc lại và khớp. Xem [publication evidence](DATABASE-PUBLICATION-20261006.json). Local đang làm việc trên main; các sửa frontend song song được giữ nguyên ngoài commit database. Hosted CI mới chưa được dùng làm bằng chứng của chặng này.

Commit database chỉ gồm source/tests/runbooks/evidence cần thiết và hai tài liệu bàn giao UI. Không gồm frontend đang sửa song song, DB/PDF/backup/log/cache/env/secret thật. Publication không phải deploy Production hoặc nghiệm thu khách hàng.
