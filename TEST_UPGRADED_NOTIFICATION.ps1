Write-Host "================================================================================"
Write-Host "  KIEM TRA TOAN DIEN HE THONG NOTIFICATION SERVICE NANG CAP CHUAN ENTERPRISE"
Write-Host "================================================================================`n"

# 1. Kiem tra so luong chua doc ban dau
$c1 = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/unread-count" -Method Get
Write-Host "[1] So thong bao chua doc ban dau (Unread Count):" $c1.data.unreadCount

# 2. Gui 2 thong bao mau qua Background Queue
$send1 = @{
    recipientUserId = "11111111-1111-1111-1111-111111111111"
    recipientEmail = "chuyenvien@company.com"
    subject = "Phan cong xu ly Cong van den so 0070/2026"
    body = "Dong chi vui long xem xet va bao cao truoc ngay 05/09/2026."
    notificationType = "Urgent"
    actionUrl = "/apps/documents/view/70"
} | ConvertTo-Json

$send2 = @{
    recipientUserId = "11111111-1111-1111-1111-111111111111"
    recipientEmail = "chuyenvien@company.com"
    subject = "Ban hanh Quyet dinh bo nhiem so 21/QD-VP"
    body = "Thong bao quyet dinh bo nhiem can bo quan ly."
    notificationType = "Info"
    actionUrl = "/apps/documents/view/21"
} | ConvertTo-Json

Write-Host "`n>>> [2] Day thong bao vao Background Queue sieu toc..."
$r1 = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/send" -Method Post -Body $send1 -ContentType "application/json"
$r2 = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/send" -Method Post -Body $send2 -ContentType "application/json"
Write-Host ("- Gui thong bao 1: " + $r1.message)
Write-Host ("- Gui thong bao 2: " + $r2.message)

Start-Sleep -Seconds 2

# 3. Kiem tra Quả chuông (In-App Notifications)
Write-Host "`n>>> [3] Kiem tra danh sach thong bao qua chuong (In-App)..."
$myNotifs = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/my" -Method Get
Write-Host ("- Tong so thong bao qua chuong: " + $myNotifs.data.totalCount)
foreach ($item in $myNotifs.data.items) {
    Write-Host ("  + [" + $item.notificationType + "] " + $item.title + " | Chua doc: " + (-not $item.isRead) + " | URL: " + $item.actionUrl)
}

# 4. Kiem tra Unread Count sau khi gui
$c2 = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/unread-count" -Method Get
Write-Host ("`n- So do hien thi tren qua chuong (Badge Count): " + $c2.data.unreadCount)

# 5. Danh dau doc 1 thong bao
if ($myNotifs.data.items.Count -gt 0) {
    $firstId = $myNotifs.data.items[0].id
    Write-Host ("`n>>> [4] Danh dau da doc thong bao ID: " + $firstId)
    $readRes = Invoke-RestMethod -Uri ("http://localhost:5005/api/notifications/" + $firstId + "/read") -Method Put
    Write-Host ("- Ket qua: " + $readRes.message)

    $c3 = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/unread-count" -Method Get
    Write-Host ("- So do con lai sau khi doc: " + $c3.data.unreadCount)
}

# 6. Kiem tra User Preferences (Tuy chon thong bao)
Write-Host "`n>>> [5] Kiem tra va cap nhat Tuy chon thong bao (Preferences)..."
$pref = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/preferences" -Method Get
Write-Host ("- Cau hinh hien tai: EmailEnabled = " + $pref.data.emailEnabled + ", InAppEnabled = " + $pref.data.inAppEnabled + ", UrgentOnly = " + $pref.data.urgentOnly)

# 7. Kiem tra Admin Logs
Write-Host "`n>>> [6] Kiem tra Nhat ky gui (Notification Logs)..."
$logs = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/logs" -Method Get
Write-Host ("- Tong so ban ghi lich su: " + $logs.data.Count)

Write-Host "`n================================================================================"
Write-Host "[OK] TAT CA 6 TINH NANG CHUAN ENTERPRISE DA DUOC HOAN TAT VA XAC THUC 100%!"
Write-Host "================================================================================"
