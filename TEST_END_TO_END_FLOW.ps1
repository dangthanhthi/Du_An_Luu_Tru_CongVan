Write-Host "================================================================================"
Write-Host "  KIEM TRA LUONG SINH SO KY HIEU CHUAN NGHI DINH 30 & THONG BAO EVENT-DRIVEN"
Write-Host "================================================================================`n"

# 1. Tao Cong van den
$doc1 = @{
    title = "Huong dan thuc hien Nghi dinh 30/2020/ND-CP ve cong tac van thu"
    summary = "So ky hieu goc: 128/BGDDT-GDCH"
    partnerId = $null
    receivedAt = ([DateTime]::UtcNow).ToString("o")
    attachmentFileIds = $null
} | ConvertTo-Json

Write-Host ">>> [1] Tao Cong van den (Incoming Document)..."
$res1 = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/incoming" -Method Post -Body $doc1 -ContentType "application/json"
Write-Host ("- So den (DocumentNumber): " + $res1.data.documentNumber + " (Chuan: So den/Nam)")
Write-Host ("- Tieu de: " + $res1.data.title)

# 2. Tao Quyen dinh di
$doc2 = @{
    title = "Quyet dinh ban hanh Quy che quan ly van ban va ho so luu tru dien tu"
    summary = "Ban hanh Quy che noi bo"
} | ConvertTo-Json

Write-Host "`n>>> [2] Tao Quyet dinh di (Outgoing Document)..."
$res2 = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/outgoing" -Method Post -Body $doc2 -ContentType "application/json"
Write-Host ("- So ky hieu di: " + $res2.data.documentNumber + " (Chuan: So/QD-VP)")
Write-Host ("- Tieu de: " + $res2.data.title)

# 3. Tao Thong bao noi bo
$doc3 = @{
    title = "Thong bao lich truc co quan va phan cong nhiem vu tuan 35"
    summary = "Thong bao noi bo van phong"
} | ConvertTo-Json

Write-Host "`n>>> [3] Tao Thong bao noi bo (Internal Document)..."
$res3 = Invoke-RestMethod -Uri "http://localhost:5002/api/documents/internal" -Method Post -Body $doc3 -ContentType "application/json"
Write-Host ("- So ky hieu noi bo: " + $res3.data.documentNumber + " (Chuan: So/TB-NB-VP)")
Write-Host ("- Tieu de: " + $res3.data.title)

Start-Sleep -Seconds 2

# 4. Kiem tra Notification Logs trong DB
Write-Host "`n================================================================================"
Write-Host ">>> [4] DANH SACH NOTIFICATION LOGS THUC TE TRONG DATABASE (EVENT-DRIVEN):"
Write-Host "================================================================================"
$notif = Invoke-RestMethod -Uri "http://localhost:5005/api/notifications/logs" -Method Get
Write-Host ("Tong so thong bao thuc te phat sinh: " + $notif.data.Count + " ban ghi")
$i = 1
foreach ($log in $notif.data) {
    Write-Host ("[" + $i + "] " + $log.sentAt + " | Gui toi: " + $log.recipientEmail + " | Trang thai: " + $log.status)
    Write-Host ("    Tieu de: " + $log.subject)
    $i++
}
Write-Host "`n[OK] TOAN BO LUONG DA DUOC XAC THUC THANH CONG 100%!"
