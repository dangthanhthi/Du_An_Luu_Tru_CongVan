# Phối hợp phiên DAS nhiều tab — checkpoint 2026-10-06T11:25:53.738188+00:00

Chặng frontend G2 đã kiểm kỹ thuật cục bộ, trạng thái **prepared**; chưa nghiệm thu G2 tích hợp thật, G7 vẫn **Partial/prepared**, G8 chưa UAT/signoff. Người dùng xác nhận thiếu dữ liệu khách hàng và kiểm chứng môi trường thật. Không báo phần trăm nghiệm thu từ số test hoặc số giai đoạn.

## Thay đổi đã kiểm

Một record `das_session_v1` lưu epoch/revision/pair/identity; Web Locks phối hợp refresh giữa context, lưu pending intent trước transport, conditional commit/invalidation và không resend token có kết quả chưa rõ. Login/logout không đợi fetch refresh; request/JSON/PDF/XLSX cũ bị chặn khi đổi owner. Exact invalidation tombstones và overflow giữ fail-closed; fresh login mới phục hồi context bị chặn.

Boundary tại layout DAS remount theo account epoch, giữ draft khi token xoay. SessionIntentContext bảo toàn owner của15 mutations qua6 facades/8 components, kể cả form con mở muộn; task send kiểm trước lưu frozen body. Cleanup dùng shared epoch acknowledgment, hoàn tất trước mount account mới, giữ pending khi browser từ chối; thông báo trễ không xóa body/idempotency key mới. React recovery effect theo cleanupReady xử lý đúng khi epoch không đổi.

Client format mới yêu cầu đăng nhập lại; hai legacy token keys không được tự nâng thành session. Metadata/menu không cấp quyền; backend vẫn là authority cho tài liệu/task, vai trò Admin và số đăng ký. Chỉ secure context/native Web Locks được hỗ trợ; browser thiếu/denied locks không có spinlock fallback. Token storage và client tests này không thay BFF/session/CSRF/revocation thật.

## Bằng chứng

- Windows Node22.23.3, locked install mới và script Prisma canonical: **291/291 frontend**, fail/skip/cancel0; typecheck/lint/full Next webpack build exit0. Lint còn66 warnings cũ,0 errors. **1.169 inputs** và inventory khớp bản frozen; sourceChanged/snapshotChanged/inventoryChanged rỗng.
- Focused9 files **130/130**; các RED thực của API/coordinator/UI/mutation/cleanup được giữ. Sáu regression stream fail trên source baseline5f6179b trước fix.
- Browser-v8: React/native Web Locks/native storage trên hai tab thật,10 observations; transport whitelist giả lập. Single refresh, draft/reload, old200/old-child409 trước POST, cleanup trước mount, delayed notifications giữ request mới, denial/recovery, logout khi refresh giữ và không resurrection. Hash runtime inputs vẫn khớp; tabs/server đã đóng.
- Python QA **93/93** qua compatibility view có source provenance; layout **10/10**; source manifest cập nhật giữ original import provenance. Final review độc lập read-only không còn important finding; reviewer không tự chạy tests hoặc xác minh live integration.

[Evidence có hash nguồn/log](CLIENT-SESSION-VERIFICATION-20261006.json). Raw logs/build trees/fixture/screenshot ngoài Git ở `.artifacts/qa/cross-tab-session-20261006`. Checkpoint tạm dừng trước đó là [lịch sử](CLIENT-SESSION-PAUSE-CHECKPOINT-20261006.md), không phải trạng thái goal hiện hành. Full check build-v7 terminal exit0; build-v5/v6 bị dừng và các lượt parity/fixture/harness lỗi không được tính như full passes.

## Publication và công việc sau

Quyền push files DAS cần thiết trực tiếp main đã được cấp; chỉ publish sau kiểm remote/ancestry và staged-byte parity, không force hoặc overwrite đồng nghiệp. Tại thời điểm ghi checkpoint này chưa publish WIP; sẽ ghi SHA và trạng thái remote sau push. Hosted CI cũ run37437948911 thuộc runtimeabb54a9, không bao phủ chặng này; workflow trước overall failure do audit6high. Package/lock/dependency không đổi; security/license gate vẫn mở, không tự chấp thuận risk.

EAP do người khác phụ trách tới khi người dùng mở lại; OCR hoãn. Giữ mentor rules: nhắc hạn **quá7ngày**, thứHai08VN; go-live2027;3 counters dùng chung loại/năm,4digits đến9999; registration date hiện tại, issued date có thể quá khứ; sửa company/phòng giữ sequence; cancelled restore trạng thái trước; My Staff DAS không tự cấp quyền công văn.

Chờ authority/session/CSRF/revocation, customer export/PDF/mapping, TMS contract/sandbox/IDs/hierarchy, SMTP/audience/directory, scanner/storage/deployment/RPO/RTO/SLA/Fax, security/audit/license/registry/signing/scanning và mentor UAT/signoff/pilot. Không customer data hoặc mail/task thật trong kiểm thử này. Goal giữ toàn bộ phạm vi, chưa complete.
