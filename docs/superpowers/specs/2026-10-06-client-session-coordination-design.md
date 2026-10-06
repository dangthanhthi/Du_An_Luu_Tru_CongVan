# G2 — phối hợp phiên DAS trong trình duyệt

Thiết kế do Codex chốt theo quyền tự quyết/ghi checkpoint người dùng đã cấp. Không mở EAP/OCR hoặc đổi authority/backend. Nguồn baseline main `5f6179b`; nghiên cứu và ba counterexample ở `docs/plans/2026-10-06-session-coordination-research.md`.

## Yêu cầu và giới hạn

1. Hai tab cùng phiên/cặp token chỉ gửi một refresh. Tab chậm được dùng token đã xoay của cùng epoch; đổi epoch tuyệt đối không replay request/body cũ bằng tài khoản mới.
2. Login/logout là thay đổi identity, refresh là thay đổi revision. Login được sắp thứ tự tại lúc ghi epoch thành công dưới mutation lock; không tuyên bố đo thứ tự click vật lý giữa process.
3. Token pair và identity hiển thị phải lưu cùng một bản ghi. Metadata không cấp quyền; backend quyết định quyền tài liệu/task và registration number.
4. Logout vô hiệu hóa local trước transport revoke, không đợi fetch refresh tab khác. Response revoke cũ không xóa phiên mới. Pending task body của tab và nội dung màn hình/file cũ phải bị loại khi đổi epoch.
5. Refresh có kết quả chưa rõ (timeout/tab đóng/mất phản hồi) không tự thử lại refresh token cũ. Người dùng đăng nhập lại; không kết luận authority chưa commit chỉ vì client timeout.
6. Hủy một caller không hủy shared refresh của caller khác. Lỗi lock, malformed record, storage quota/denial phải fail closed, không khôi phục token đã xoay. Nếu cả ghi/xóa storage bị từ chối không thể hứa vô hiệu hóa mọi tab; tab khởi tạo phải từ chối dùng phiên và gate revocation thật vẫn mở.
7. Rà response 200, JSON body, PDF/XLSX stream và UI khi epoch thay đổi trong lúc đang chờ; không chỉ kiểm401. Mutation có outcome chưa rõ không tự resend.
8. Node22, TypeScript hiện hành, không thêm dependency. Chỉ HTTPS/secure context có Web Locks được dùng để chia sẻ phiên; không hỗ trợ/denied locks không có fallback spinlock. SSR không nhận phiên từ localStorage.
9. Chưa có customer data/authority/TMS/SMTP/scanner thật. Test giả lập/browser fixture không thay UAT, license/security audit hoặc nghiệm thu tích hợp. EAP thuộc người khác, OCR hoãn.

## Format và phối hợp

Key `das_session_v1` chứa schema version1, authority URL, UUID epoch, safe-integer revision, state `anonymous|active|refreshing`, token pair, identity JSON hoặc null và pending UUID hoặc null. Anonymous chỉ giữ epoch, revision0, token/user/pending null. Active/refreshing có cặp token không rỗng, ASCII không whitespace, mỗi token tối đa16KiB; toàn record tối đa64KiB. Parser từ chối version/authority/shape sai và legacy keys không được tự nâng cấp thành phiên có quyền. Khi triển khai client format mới cần đăng nhập lại; không migrate dữ liệu nghiệp vụ.

`das.auth.session.v1` là mutation lock cho các commit đồng bộ ngắn. `das.auth.refresh.v1:<epoch>` là refresh lock. Refresh có thể lấy refresh lock rồi mutation lock; login/logout chỉ lấy mutation lock, không có fetch trong mutation lock. Queue deadline5s; refresh network/body deadline15s. Deadline lock không được coi là hủy fetch đã được cấp; fetch có controller và race deadline riêng.

Trước network refresh, dưới mutation lock lưu trạng thái `refreshing` với pending UUID của lần thử, giữ epoch/revision/cặp cũ. Chỉ sau khi intent lưu thành công mới gọi authority. Commit kiểm lại epoch, revision, pair và pending UUID, ghi `active` với revision+1 và pair mới trong một setItem. Nếu next holder của refresh lock thấy intent còn treo, đó là outcome chưa rõ/crash: invalidation và yêu cầu login; không gọi lại token cũ. Epoch có thể refresh nhiều lần, dedup theo revision/pair chứ không giới hạn một rotation suốt phiên.

Invalidation conditional dưới mutation lock chỉ xóa đúng phiên/revision đang sở hữu. Thử ghi anonymous epoch mới; nếu quota fail, conditional remove key là fallback. Nếu cả hai thất bại, context ghi nhớ epoch bị chặn và trả lỗi, không tự tuyên bố global logout. Successful pending intent còn lại ngăn tab khác tự gửi lại token đã có outcome chưa rõ. Metadata/cache/key legacy được dọn khi đổi identity; storage event chỉ thông báo, không thay mutual exclusion.

Nếu invalidation không thể đọc storage, context vẫn ghi nhớ chính xác commit bị từ chối (epoch/revision/pair/state/pending), không chặn revision mới hoặc phiên thay thế. Logout không xác định được owner phải giữ trạng thái unresolved thay vì cho phiên cũ sống lại khi storage phục hồi. Hai tập chặn giới hạn32 entry; vượt giới hạn đóng context, không FIFO-evict credential đã từ chối. Chỉ fresh anonymous login được ghi thành công mới reset các điều kiện chặn; metadata/token cache không phải cơ chế phục hồi.

## API và UI

`createSessionCoordinator(authority, platform)` nhận storage, Web Locks, UUID, notification và deadline; mỗi context có instance riêng, storage/locks có thể chung. `snapshot/read`, `beginLogin`, `commitLogin`, `invalidate`, `logout`, `refresh` là các điểm truy cập. Public tokenManager chỉ đọc; code production không ghi trực tiếp token/user. Fixture seed record chỉ nằm trong tests.

Auth login ghi anonymous epoch trước network và commit whole-session có điều kiện; response/error cũ không làm thay đổi phiên mới. Logout lấy token để revoke trong mutation lock, invalidate trước transport, không clear sau transport. API request chụp epoch; refresh/retry giữ epoch, response 200/parsed body/stream kết thúc trên epoch khác trả conflict và không đưa dữ liệu cũ vào UI. Các wrapper giữ lỗi session riêng, không biến thành lỗi mạng giả.

Boundary tại layout DAS nhận storage/same-tab event/focus, đọc epoch, bỏ/remount nội dung và dọn task bodies khi identity đổi. Refresh cùng epoch không remount hoặc mất draft. Hints/menu phản ánh record mới; không cấp quyền qua cache. Request đang chờ với epoch cũ không được dùng để cập nhật màn hình mới.

Boundary cung cấp SessionIntentContext theo epoch đang hiển thị. Form con mở sau khi storage đã đổi vẫn kế thừa owner của giao diện cha; không tự chụp tài khoản mới từ storage. Hook ngoài provider giữ owner anonymous. 15 điểm mutation hiện hành của tám giao diện truyền intent qua sáu facade, kiểm trước transport; task send kiểm trước khi lưu frozen body. API bỏ tùy chọn sessionIntent trước khi gọi fetch, giữ intent hợp lệ khi token xoay cùng epoch.

React có thể thấy snapshot mới trước thông báo storage. Boundary gate replacement children đến khi layout effect xác nhận cleanup, rồi mới mount chúng; first mount/reload cùng phiên không xóa frozen request. Tất cả subscription/menu/boundary dùng tracker epoch chung trong một context để thông báo trễ không xóa body/key mới. Cleanup bị từ chối giữ pending, chặn authenticated content/intent/transport/retry/delivery và được thử lại khi focus/storage event đến sau phục hồi; chỉ cleanup thành công được acknowledge. Dependency cleanupReady làm layout effect chạy lại khi quyền storage phục hồi dù epoch không đổi. Không tuyên bố xóa toàn bộ storage hoặc revoke authority khi browser vẫn từ chối quyền.

## Kiểm chứng và bàn giao

RED đầu tiên phải chạy hai module API thực độc lập trong VM chung storage/lock fixture trên code cũ, tái hiện refresh kép, login resurrect sau logout, login cũ overwrite login mới, 401 muộn sau cùng-epoch rotation và 200/body từ actor cũ. Tiếp theo test coordinator: pending crash/timeout, quota/denied, unsupported locks, lock ordering, revision lần2, aborted waiter, replacement survival.

Test browser thật trên loopback fixture riêng kiểm Web Locks/storage event, hai tab, logout không chờ network và cleanup; không gọi authority/mail/task thật hoặc để frontend preview chạy sau QA. Typecheck/lint/full build/hồi quy/manifest/source parity phải có evidence. Chỉ publish files DAS cần thiết sau review và kiểm remote/ancestry; không force push. G2 chỉ được đánh prepared core khi đã wired API, không coi module unused hoặc spec là cơ chế đã hoàn thành. UI/browser/authority gates có scope riêng và còn mở nếu thiếu evidence.
