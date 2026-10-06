# DAS — GitHub publication và hosted CI ngày 06/10/2026

Đã push fast-forward `main` từ `d519363` tới **`abb54a91e2c9f73ea8b4344d0c7c3182fb11ce51`**, gồm các sửa 7 ngày, session/quyền/preflight/intake và SQL QA đã kiểm. Không force push, không thay công việc đồng nghiệp; raw log/cache/backup/PDF/credential/tài liệu mentor ở ngoài Git. Các commit tài liệu tiếp theo không đổi runtime nguồn này.

Đã dispatch một lần workflow và kiểm kết quả thật: [run37437948911](https://github.com/dangthanhthi/Du_An_Luu_Tru_CongVan/actions/runs/37437948911). **5 job chức năng qua; cả workflow vẫn failure do dependency audit**. Xem [evidence](HOSTED-CI-VERIFICATION-20261006.json).

| Job | Kết quả đã xác minh |
|---|---|
| layout-qa | Success; cả bước layout và Python QA qua. Không tự gán số test hosted khi job này không upload raw QA report |
| backend Ubuntu | 629/629 non-SQL, Gateway Release build qua; 8 TRX không fail/skip |
| backend Windows | 629/629 non-SQL, Gateway Release build qua; 8 TRX không fail/skip |
| frontend Ubuntu | TAP223/223, typegen/typecheck/lint/full build qua, không fail/skip/cancel |
| SQL QA Ubuntu | 75 core +load1+restore1 =77 test, 7 TRX không fail/skip; 300 registrations +300 replay, 5 backup +PDF, bundle6artifacts qua, cleanup confirmed |
| dependency audit | Failure tại npm-audit: 6 high; 6 core NuGet service audit không finding |

Đã tải 5 artifact báo cáo, đối chiếu ZIP SHA256 với digest API, đọc và kiểm TRX/TAP thay vì chỉ tin trạng thái job. SQL manifest362 inputs khớp Git archive `abb54a9` và nguồn runtime hiện tại, sourceChanged rỗng; không upload `.bak`/PDF/cache. Docker `.Id` quan sát ở hosted khác máy Windows; runner giữ kiểm RepoDigest cố định `mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`, không thay sang tag trôi nổi.

Audit chỉ ra `braces <=3.0.3` và chuỗi dev tool Next ESLint/globby/stylelint/fast-glob/micromatch. Tại lần kiểm ngày06/10/2026, advisory chưa công bố patched version. Giữ lockfile và full audit failure, chưa ký chấp nhận rủi ro. [GitHub Advisory Database](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)

Đã kiểm thêm local `npm audit --omit=dev`: 0 findings, exit0 và package/lockfile không đổi, khớp commit đã publish. Đây là bằng chứng riêng của graph runtime hiện hành, không đóng gate full audit hoặc license. Local Python93/93/layout10/10/SQL runner14/14 giữ đúng scope riêng.

## Tiếp tục

G7 vẫn **Partial/prepared**, G8 chưa UAT/signoff. Hosted functional CI nay đã có bằng chứng; security/full-pipeline, license, registry/signing/scanning, triển khai/RPO/RTO/SLA vẫn còn. Chờ authority/session thật, TMS API/sandbox/mapping, SMTP/audience, customer DB/PDF/mapping, scanner/storage, Fax thật và mentor UAT/pilot. Không dùng test giả lập để nghiệm thu các đầu vào này.

Mốc nhắc chính thức **quá7 ngày**, thứHai08VN; EAP do người khác phụ trách và chỉ mở lại khi người dùng yêu cầu rõ, OCR hoãn. Không SMTP/TMS/customer data thật trong CI. Không dispatch lại workflow đang có chỉ để lặp bằng chứng; chỉ rerun khi đổi input hoặc sửa failure.

Nguồn chính `DAS-Collaboration`, nhánh local `codex/das-completion-20261006`. Các ghi chú trước đây “chưa push/hosted CI chưa chạy” là lịch sử trước publication. Khi tiếp tục, kiểm `git status`, remote main và source SHA; không phát triển ở worktree cũ. Raw artifacts và API state trong `.artifacts/qa/github-ci-20261006` và `github-ci-dispatch-20261006.json`, ngoài Git. `gh` không có trong PATH; API dùng cached Git authorization trong bộ nhớ, không in/ghi token hoặc đọc `.env` của nhóm Task Management.
