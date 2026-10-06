# Client Session Coordination Implementation Plan

> **For agentic workers:** Use executing-plans inline, task-by-task with independent review. Concurrency/security implementation stays with main Codex under repository policy.

**Goal:** Hoàn chỉnh phối hợp phiên DAS giữa nhiều context mà không replay request hoặc hiển thị dữ liệu của tài khoản cũ.

**Architecture:** Một record JSON epoch/revision, mutation lock ngắn và refresh lock riêng. Lưu pending intent trước authority transport, conditional whole-record commit/invalidation, ownership checks ở response/body/stream và UI boundary.

**Tech Stack:** TypeScript, Web Storage/Web Locks, Next.js/React, Node22 node:test/VM; không thêm dependency.

**Spec:** `docs/superpowers/specs/2026-10-06-client-session-coordination-design.md`.

## Global Constraints

- EAP thuộc người khác, OCR hoãn; không customer data/credential/authority/mail/task thật.
- Queue5s, refresh network/body15s; token<=16KiB, record<=64KiB; schema1 key das_session_v1.
- Không storage spinlock/CAS giả, không retry token outcome chưa rõ, không replay sang epoch mới.
- Giữ backend authority/quyền/registration business rules; không tự đóng security/license/UAT.
- Source/node_modules/artifacts/test browser phải có ownership và scope riêng; không mutate snapshot QA trước.

### Task1 — Core coordinator và API integration

**Files:** Create `frontend/src/services/sessionCoordinator.ts`, `frontend/tests/frontend/cross-tab-session.test.ts`, `frontend/tests/frontend/helpers/session-fixture.ts`; modify `frontend/src/services/api.ts`, legacy/session/document-task tests.

**Interfaces:** Factory nhận platform cho context độc lập; API instance chỉ dùng browser platform runtime. TokenManager getters đọc single record. Login/refresh/logout dùng coordinator.

- [x] Viết và chạy RED hai module API hiện hành trong VM; chứng minh lỗi ở API thực.
- [x] Viết factory/parser/short mutation locks/pending refresh/network deadline và coordinator regressions.
- [x] Wire auth API và fetch ownership; bỏ setters production, cập nhật fixture seed, giữ regression intents cũ khi format đổi.
- [x] Chạy focused/core/legacy tests, review độc lập; ghi evidence source scope. Không đánh task xong nếu coordinator chưa wired.

### Task2 — Response/body/stream và UI epoch

**Files:** Modify API JSON/PDF/XLSX handlers, menu/launcher hooks/layout DAS; create boundary và test ownership/browser fixture.

- [x] RED old-epoch200, delayed parsed body, PDF/XLSX stream và UI reset; không chỉ401.
- [x] Wire ownership sau await/body/signature, typed conflict, conditional401 clear; dọn/remount UI chỉ khi epoch đổi, không khi revision xoay.
- [x] Giữ draft/task reconciliation không auto-resend, abort waiter không cancel shared refresh.
- [x] Review/test cả null/unsupported/denied storage và cleanup; chốt source boundary thực từ layout repository.
- [x] Inherited owner cho form con, ownership ở15 mutations và chặn trước frozen body/transport; exact invalidation tombstones/overflow fail-closed.
- [x] Cleanup trước mount, shared acknowledgment cho delayed notifications, pending cleanup khi denial và React recovery dependency; RED rồi GREEN với harness có đúng dependency/commit ordering.

### Task3 — Browser, full verification và publication

**Files:** QA fixture/scripts, checkpoint/evidence, source-layout manifest và collaboration status.

- [x] Kiểm browser thực hai tab qua fixture loopback, không transport thật; close tabs/service thuộc QA.
- [x] Frozen source/install Node22; full frontend tests/typecheck/lint/build; Python/layout khi relevant.
- [x] Hash parity/source integrity và independent review; ghi failed attempts với nguyên nhân, không tính thành pass.
- [x] Chỉ publish files DAS cần thiết lên main fast-forward sau remote check, lưu desktop cache/evidence. Hosted CI cũ không chứng minh patch mới; ghi gate còn mở.

Task1/2 và browser scope có evidence ở checkpoint tạm dừng ngày06/10: focused130/130, browser-v8 với React/native Web Locks/storage thật nhưng transport giả lập; source hashes vẫn khớp sau khi tiếp tục. Task3 full verification build-v7 đã qua291/291/typecheck/lint/build/source parity, Python93/layout10; runtime87ddc9c đã publish main fast-forward sau remote/staged parity; bố cục máy/GitHub và desktop cache đã đồng bộ. Hosted CI mới chưa dispatch, CI cũ không bao phủ runtime này. build-v5/v6 bị dừng theo yêu cầu người dùng và không tính là full passes. Xem docs/CLIENT-SESSION-CHECKPOINT-20261006.md và evidence cùng chặng. Không suy phần trăm dự án hoặc đóng G7/G8 từ số test/checklist này.
