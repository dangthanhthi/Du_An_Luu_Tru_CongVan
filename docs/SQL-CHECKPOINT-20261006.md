# DAS — checkpoint SQL QA ngày 06/10/2026

> Sau checkpoint SQL: đã publish runtime `abb54a9` lên main và chạy hosted CI thật. 5 job chức năng qua (SQL77, backend629 mỗi OS, frontend223, layout/QA); full pipeline failure do audit6high. Xem [checkpoint GitHub/CI](HOSTED-CI-CHECKPOINT-20261006.md). Các trạng thái local/chưa dispatch dưới đây là thời điểm trước publication.

Đã hoàn thiện runner SQL cho layout cộng tác. **75/75 core SQL tests**, 1 load fixture và 1 restore fixture qua trên Windows .NET10.0.204 với SQL Server 2022 thật trong Docker riêng. Load kiểm 300 đăng ký + 300 exact replays, counters chung 3 loại, không duplicate/gap và conflict không cấp số. Restore kiểm backup/restore 5 store + PDF giả lập, số 4/5 chữ số, công ty/phòng sau sửa, token thu hồi/tombstone/audit/correlation và không gửi lại SMTP chưa rõ kết quả.

Bằng chứng tổng hợp: [SQL-VERIFICATION-20261006.json](SQL-VERIFICATION-20261006.json). Cách chạy lại: [SQL-QA.md](SQL-QA.md). Quy tắc nhắc hạn vẫn **quá 7 ngày**, đúng 7 ngày chưa đủ, thứ Hai 08:00 Việt Nam. EAP do người khác phụ trách, OCR tiếp tục hoãn.

## Đã sửa và kiểm chứng

- `tools/qa/run-isolated-sql.py` có core/load/restore/all, chỉ tạo instance mới, không nhận connection string hay resource có sẵn. Pinned image official, nonroot, giới hạn RAM/CPU, mật khẩu QA tự sinh qua environment, loopback port, bridge riêng, không host/socket mount. Bridge không được tuyên bố chặn egress. Environment test loại credential/connection/transport kế thừa, tắt worker và dùng NuGet public config/cache riêng.
- Hai fixture load/restore nhận root canonical `tools/qa/` và compatibility `scripts/qa/`; không còn tìm `.ps1` không được đưa vào bản cộng tác. Manifest nguồn được cập nhật hash hiện hành, original import provenance giữ lại.
- TRX phải có test thực chạy/pass, không fail/skip; nguồn được đối chiếu hash. Cleanup chỉ xóa tài nguyên khớp label/name/ID lượt hiện tại và xác nhận đã mất. Không coi lỗi inspect/daemon là đã dọn.
- Bundle giữ cutId của fixture, 5 `.bak` + một PDF; verifier v2 đối chiếu checksum/artifact set. `integrityPassed=true`, 6 artifacts, `mutated=false`, `productionReady=false`. Số lần SMTP resend giả lập = 0.
- Focused runner **14/14**; Python QA compatibility **93/93**, layout **10/10**, không skip. Review độc lập read-only hai lượt không có lỗi actionable; Codex kiểm lại actual TRX/source hashes và cleanup.
- CI job SQL được chuẩn bị trong `core-ci.yml`, chỉ workflow_dispatch, pinned actions/image, token contents-read, không upload backup/cache/PDF. YAML đã parse/kiểm điều kiện; **chưa chạy hoặc chấp nhận hosted CI**.

## Bằng chứng và các lượt lỗi được giữ

`all v5` terminal session31437 exit0: core75 + load1 + restore1, không source change, cleanup qua. Sau thêm bundle verifier, `restore v6` terminal session93699 exit0: restore1 + checksum bundle6 artifacts, nguồn giữ nguyên, cleanup qua. Python/layout session21004 exit0. Không cộng lượt restore lặp để tăng số test độc lập: vẫn **77 test SQL/load/restore độc lập**.

V5 chạy trước bổ sung helper bundle; v6 chạy runner cuối. Toàn bộ input ứng dụng/migration/workflow/fixture của v5/v6 khớp byte với hiện tại. Chỉ `tools/qa/run-isolated-sql.py` khác ở snapshot v5; snapshot v6 khớp. Đây không phải assertion mọi file repo hoặc CI hosted đã kiểm.

V1/v2 lỗi khởi động do executable image có file capability `cap_net_bind_service=ep`; v3 không có port binding trên internal network; v4 config NuGet có phần credential-clear không hợp lệ. Đã sửa nguyên nhân, giữ report lỗi và xác nhận cleanup từng lượt. Test bundle RED: thiếu helper gây 7 errors/subtests; GREEN 14/14. Không dùng lượt lỗi làm evidence đã pass.

Raw logs/TRX/source manifests/backups/PDF/cache giữ ngoài Git trong `.artifacts/qa/sql-canonical-20261006-v5`, `-v6`, Python QA và `sql-runner-red-bundle.log`. Không còn container/network thuộc label SQL QA sau lượt cuối; không đụng các container khác trên máy.

## Trạng thái và việc tiếp theo

G7 vẫn **Partial/prepared**, G8 chưa UAT/signoff. SQL ở đây là SQL thật với dữ liệu giả lập trên instance riêng; không apply migration hoặc restore lên khách hàng. Các kết quả non-SQL629/frontend223/preflight26 ở checkpoint trước giữ đúng scope; phần này không sửa runtime nghiệp vụ.

Tiếp tục hosted clean-clone CI/publication trên repo được người dùng chỉ định, kiểm remote/branch trước và không overwrite công việc đồng nghiệp. Còn browser/gateway/authority/session, TMS sandbox/API/mapping, SMTP/audience, customer DB/PDF/mapping, scanner/storage, triển khai/RPO/RTO/SLA, security/license, Fax và mentor UAT/pilot. Đầu vào thật chưa có phải giữ prepared, không tự tạo credential hoặc ký nghiệm thu.

Nguồn chính `DAS-Collaboration`, nhánh `codex/das-completion-20261006`; mốc trước chặng SQL `af3a2e39043a4dd23c6753e26a50c83494bb5195`. Đọc `git log -1` và checkpoint tiếp tục trên Desktop để biết commit sau chặng. Ở thời điểm chạy SQL trước dispatch, các commit mới còn local; trạng thái publication sau đó được ghi trong checkpoint GitHub/CI nêu trên.
