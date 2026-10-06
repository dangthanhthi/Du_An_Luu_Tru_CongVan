# G2 — hướng xử lý phiên đăng nhập giữa nhiều tab

Trạng thái: nghiên cứu và yêu cầu cho chặng tiếp theo, **chưa triển khai hoặc nghiệm thu cơ chế nhiều tab**. Chặng sửa lỗi lưu refresh/menu logout có bằng chứng riêng trong `SESSION-STORAGE-CHECKPOINT-20261006.md`.

## Yêu cầu phải giữ

- Một lần refresh cho cùng phiên khi nhiều tab nhận 401 đồng thời; tab đến sau được dùng kết quả đã lưu của đúng phiên.
- Đăng nhập mới/đăng xuất phải vô hiệu hóa phản hồi cũ; không gửi lại body của tài khoản cũ bằng token của tài khoản mới.
- Cặp access/refresh và metadata hiển thị phải được đọc/ghi cùng một bản ghi; quyền thật vẫn do backend quyết định.
- Đăng xuất cục bộ phải hoàn tất trước transport revoke và không chờ hết một fetch refresh đang chậm ở tab khác.
- Lỗi lưu trữ/lock/timeout không được khôi phục refresh token đã bị authority xoay; không tự resend một mutation có kết quả chưa rõ.
- Hủy một request không hủy refresh mà request khác còn dùng. Pending task body, dữ liệu màn hình và file tải phải theo đúng phiên/tài khoản.
- EAP/OCR vẫn ngoài phạm vi; không thay authority, không triển khai BFF hoặc nghiệm thu tích hợp thật trong chặng này.

## Nguồn đã đối chiếu ngày 06/10/2026

HTML Standard yêu cầu tác giả không giả định có locking giữa các agent cluster khi dùng localStorage; thao tác đọc rồi ghi không phải compare-and-swap. Vì vậy số generation trong module và hai key token hiện nay chưa chứng minh an toàn giữa các tab. [Web storage — WHATWG](https://html.spec.whatwg.org/multipage/webstorage.html).

Web Locks có thể phối hợp các context cùng storage bucket. Lock được giữ đến khi promise của callback kết thúc; API được định nghĩa trong secure context. AbortSignal của yêu cầu lock chỉ giải quyết phần chờ cấp lock; fetch cần deadline riêng. Tài liệu W3C đang là Working Draft, không dùng nó làm bằng chứng browser thực tế đã được kiểm. [Web Locks — W3C](https://www.w3.org/TR/web-locks/).

## Phương án ưu tiên cần kiểm chứng

Một bản ghi JSON có version, epoch ngẫu nhiên, authority, access token, refresh token và identity hiển thị. Mỗi login/logout đổi epoch; refresh giữ epoch. Không dùng counter localStorage làm epoch và không tự nhận phiên từ hai key legacy là bản ghi mới hợp lệ.

Hai loại Web Lock với thứ tự cố định:

1. Lock mutation phiên chỉ bao quanh đọc/kiểm tra/ghi đồng bộ. Login bắt đầu/commit, logout và refresh commit cùng dùng lock này; không fetch trong lock này.
2. Lock refresh theo epoch bao quanh refresh transport, có deadline. Khi được cấp lock, đọc lại bản ghi: epoch thay đổi thì không retry; epoch giữ nguyên và token đã xoay thì dùng kết quả tab trước; chưa xoay thì gọi refresh. Commit phải lấy lock mutation và kiểm lại epoch/tokens.

Chỉ refresh có thể lấy lock refresh rồi lock mutation. Login/logout không chờ lock refresh, tránh vòng chờ và tránh trì hoãn logout bởi mạng. Không đưa token vào tên lock, log hoặc URL.

Không dùng spinlock localStorage hoặc sự kiện storage làm bằng chứng mutual exclusion. Trình duyệt không hỗ trợ/không cho phép Web Locks phải có hành vi fail-closed được định nghĩa và kiểm riêng trước khi chuyển format. Chưa chốt bằng test khả năng tương thích trình duyệt thực tế; không tự thêm fallback không an toàn.

## Các điểm review yêu cầu giải quyết trước implementation

1. Một epoch có thể refresh hợp lệ nhiều lần. Dedup phải theo epoch **và revision/cặp token**, không coi một lock theo epoch là bằng chứng chỉ có một rotation suốt phiên. Nếu authority đã xoay token nhưng response mất hoặc tab đóng, tab sau không được tự gửi lại token cũ. Spec cần trạng thái intent/outcome được lưu trước transport và chính sách hết hiệu lực khi outcome chưa rõ; không tuyên bố timeout nghĩa là authority chưa commit.
2. Nếu ghi epoch/tombstone login-start/logout thất bại, epoch cũ vẫn có thể còn ở storage của tab khác. Lock không sửa được lỗi durable invalidation. Phải kiểm write failure và conditional removal, từ chối dùng phiên trong tab khởi tạo nếu không xác nhận vô hiệu hóa; không hứa vô hiệu hóa tất cả tab khi cả ghi/xóa đều bị từ chối. Hành vi revoke/outcome cần được ghi rõ, không thay bằng storage event giả.
3. Thứ tự login phải được xác lập tại mutation lock khi đăng ký epoch thành công; không thể đo “thời điểm click vật lý mới nhất” giữa hai process bằng generation module. Test phải bao phủ login đang chờ lock và login-start không lưu được, không chỉ response đảo thứ tự.

Review đọc độc lập nêu ba điểm này; chưa chạy kiểm chứng hai-tab hoặc authority. Đây là các điều kiện còn mở của thiết kế, không phải cơ chế đã có trong code.

## Các bước thực hiện tiếp

- [ ] Rà tất cả getter/setter token, auth caller, cache/draft, hook và nơi giữ dữ liệu màn hình; chốt API đọc/ghi và chuyển format.
- [ ] Viết spec/implementation plan với hành vi unsupported/denied storage, epoch, deadlines, outcome của mutation và clearing UI rõ ràng; review độc lập.
- [ ] Tái hiện bằng hai module API độc lập cùng storage/lock manager: rotation đồng thời, 401 muộn, login đảo thứ tự, logout trong refresh và quota failure.
- [ ] RED trước khi thay implementation; kiểm cùng-epoch rotation được dùng lại nhưng đổi-epoch tuyệt đối không replay.
- [ ] Bao phủ parsing body/stream PDF/XLSX và màn hình khi tài khoản đổi trong lúc request còn chạy, không chỉ trạng thái 401.
- [ ] Kiểm hai tab trình duyệt thực trên fixture loopback riêng có quản lý cleanup; xác minh feature probe/lock release/timeout. Không cần dữ liệu hay credential khách hàng, không để preview chạy sau QA.
- [ ] Typecheck/lint/build/hồi quy, source parity, checkpoint/evidence; chỉ sau đó cập nhật main theo quyền đã được người dùng cấp.

Các bước trên chưa được đánh hoàn thành bằng kết quả test một module của chặng hiện tại. Authority/session/CSRF/revocation thật, customer migration và UAT vẫn chờ đầu vào được bàn giao.
