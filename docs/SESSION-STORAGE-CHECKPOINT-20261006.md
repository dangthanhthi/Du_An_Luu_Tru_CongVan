# G2 — lỗi lưu refresh và đăng xuất từ menu, 06/10/2026

Đã sửa hai lỗi trong luồng DAS hiện có:

- Khi authority trả token refresh hợp lệ nhưng ghi access/refresh vào localStorage thất bại, phiên cũ/dở dang thuộc request đó bị xóa. Caller giữ phản hồi 401 ban đầu và không replay POST. Không khôi phục refresh token đã bị xoay; guard generation và token pair giữ phiên thay thế.
- Menu đăng xuất không gọi clearTokens lần thứ hai sau khi chờ logout transport. API đã xóa cục bộ trước transport, nên phản hồi logout muộn hoặc lỗi mạng không xóa phiên đăng nhập mới.

## Bằng chứng hiện hành

- RED: 8 regression mới thất bại trên implementation cũ; 18 test cũ qua. Lượt RED bổ sung kiểm trực tiếp access token còn sót lại, không chỉ thông báo lỗi.
- GREEN: **26/26** session/menu tests qua. Test menu lấy handler thật bằng TypeScript AST và chạy với authApi thật; không render MUI hoặc giả API logout.
- Frontend Windows Node **22.23.3**: **231/231**, không fail/skip/cancel; typecheck, lint và full Next.js webpack build exit0. Lint toàn bộ còn 66 warning cũ, 0 error; lint ba file sửa không có warning/error.
- Python QA **93/93**, layout **10/10** qua. Hash nguồn hiện hành, bản nguồn build riêng và package/lock khớp; sourceChanged/snapshotChanged rỗng. Manifest cập nhật đúng ba nguồn sửa, giữ original import provenance.
- Review code đọc độc lập không có actionable finding; reviewer không chạy test. Review nghiên cứu nhiều tab nêu ba điều kiện thiết kế còn mở, đã ghi vào [nghiên cứu tiếp theo](plans/2026-10-06-session-coordination-research.md).

[Evidence có hash nguồn/log](SESSION-STORAGE-VERIFICATION-20261006.json). Raw log/dependencies/build trees chỉ ở `.artifacts/qa/session-storage-20261006/`, ngoài Git. Không giữ frontend server hoặc gửi email/task thật.

Hai lỗi bộ chạy QA tạm đã giữ bằng chứng: lượt full suite dùng NODE_PATH không resolve được ESM `@iconify/tools`; lượt build đầu gọi Prisma trực tiếp thay vì script staging canonical. Không tính chúng là lượt qua. Bản build v2 dùng source/install mới, lockfile và script `frontend/scripts/generate-prisma.cjs`, qua đầy đủ và không đổi source.

## Phạm vi chưa hoàn tất

Đây là rollback và sửa logout UI, **không phải atomic rotation giữa nhiều tab**. Cặp token vẫn lưu ở hai key và generation vẫn thuộc module. Chưa kiểm hai tab browser, Web Locks/shared epoch, response 200/stream/UI khi đổi tài khoản, BFF/authority/session/CSRF/revocation thật.

CI hosted run **37437948911** là evidence của runtime **abb54a9**, không bao phủ patch này. Workflow đó overall failure vì audit6high; package/lock hiện hành không đổi, không tự đóng security/license gate hoặc chấp thuận rủi ro.

G7 vẫn Partial/prepared, G8 chưa UAT/signoff. Người dùng xác nhận còn thiếu thông tin dữ liệu khách hàng/môi trường thật; customer migration/live verification giữ chờ đầu vào. Tiếp tục công việc DAS có thể kiểm độc lập, không tự tạo dữ liệu hay nghiệm thu thay mentor. EAP thuộc người khác đến khi user mở lại, OCR vẫn hoãn. Mốc nhắc hạn giữ **quá 7 ngày**, thứ Hai 08:00 giờ Việt Nam.

## Bước tiếp theo

Hoàn chỉnh spec nhiều tab từ các counterexample đã review, viết RED bằng hai module độc lập và kiểm browser fixture riêng. Không mặc định localStorage read/write là CAS; định nghĩa rõ refresh outcome chưa rõ, durable invalidation lỗi và điểm xác lập thứ tự login trước khi viết implementation. Quyền push các file DAS cần thiết lên main đã được user cấp; kiểm remote/ancestry, không force push hoặc overwrite công việc đồng nghiệp.
