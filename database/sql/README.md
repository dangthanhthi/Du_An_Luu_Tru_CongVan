# SQL Server scripts và kiểm toàn vẹn gói

Sáu script `auth.sql`, `document.sql`, `files.sql`, `notification.sql`, `partner.sql`, `email.sql` được xuất bằng `migrations script --idempotent` của đúng DbContext. Chúng chứa DDL, migration history và seed tham chiếu của migration, không chứa dữ liệu khách hàng hoặc credential.

Đọc [PROVISIONING.md](../PROVISIONING.md) để chạy vào sáu database mới riêng. Scripts dùng `GO`; dùng SSMS/sqlcmd hoặc runner có hỗ trợ batch, không gửi cả file vào một SqlCommand đơn lẻ.

Đây là bộ database bàn giao độc lập. Không dùng sáu scripts như một phương án upgrade database có sẵn khi chưa đối chiếu lịch sử và dữ liệu.

## Kiểm hash từ root repository

PowerShell dưới đây chỉ đọc file, không kết nối database:

```powershell
$taskPackageRoot = (Resolve-Path -LiteralPath database).Path
$taskPackageManifest = Get-Content -LiteralPath database/PACKAGE-MANIFEST.json -Raw | ConvertFrom-Json
foreach ($taskFile in $taskPackageManifest.files) {
    $taskFilePath = Join-Path $taskPackageRoot $taskFile.path
    $taskActualHash = (Get-FileHash -LiteralPath $taskFilePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($taskActualHash -ne $taskFile.sha256) { throw "Hash mismatch: $($taskFile.path)" }
    if ((Get-Item -LiteralPath $taskFilePath).Length -ne $taskFile.bytes) { throw "Size mismatch: $($taskFile.path)" }
}
"Verified $($taskPackageManifest.files.Count) database files"
```

Manifest không tự hash chính nó; Git commit giữ phiên bản manifest. Nếu Git tự đổi line ending khi checkout, byte hash sẽ khác dù text giống nhau. Thư mục database có `.gitattributes` yêu cầu giữ nguyên bytes của gói khi checkout. Mỗi migration ID, tool version và hash script cũng nằm trong manifest.

Lượt xuất SQL chỉ kiểm model/snapshot và sinh script offline; không kết nối hoặc áp dụng vào DB. Bằng chứng này không thay test SQL thực thi/upgrade/UAT sau khi đồng bộ backend chính thức.
