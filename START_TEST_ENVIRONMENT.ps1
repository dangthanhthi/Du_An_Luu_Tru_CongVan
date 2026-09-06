# Start all microservices in background
$baseDir = "c:\Users\MSIIIIII\Desktop\Dự án taskmanager\Intern-DocumentAdministration-BE"
$feDir = "c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS-Frontend"

# Kill any lingering dotnet processes on target ports if any
Get-Process -Name "AuthService", "DocumentService", "PartnerService", "FileService.API", "NotificationService", "ai-ocr-service", "EmailWorkerService", "Gateway" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Starting AuthService (5001)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5001" -WorkingDirectory "$baseDir\services\auth-service" -WindowStyle Hidden

Write-Host "Starting DocumentService (5002)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5002" -WorkingDirectory "$baseDir\services\document-service" -WindowStyle Hidden

Write-Host "Starting PartnerService (5003)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5003" -WorkingDirectory "$baseDir\services\partner-service" -WindowStyle Hidden

Write-Host "Starting FilesService (5004)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5004" -WorkingDirectory "$baseDir\services\files-service" -WindowStyle Hidden

Write-Host "Starting NotificationService (5005)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5005" -WorkingDirectory "$baseDir\services\notification-service" -WindowStyle Hidden

Write-Host "Starting AI-OCR Service (5006)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5006" -WorkingDirectory "$baseDir\services\ai-ocr-service" -WindowStyle Hidden

Write-Host "Starting EmailWorkerService (5007)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:5007" -WorkingDirectory "$baseDir\services\email-worker-service" -WindowStyle Hidden

Write-Host "Starting Gateway (8080)..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList "run --urls http://localhost:8080" -WorkingDirectory "$baseDir\gateway" -WindowStyle Hidden

Write-Host "Starting Frontend (3000)..." -ForegroundColor Cyan
Start-Process -FilePath "npm" -ArgumentList "run dev" -WorkingDirectory "$feDir" -WindowStyle Hidden

Write-Host "Waiting 12 seconds for all services to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 12

# Health check all services
$services = @(
    @{ Name = "AuthService"; Url = "http://localhost:5001/health" },
    @{ Name = "DocumentService"; Url = "http://localhost:5002/health" },
    @{ Name = "PartnerService"; Url = "http://localhost:5003/health" },
    @{ Name = "NotificationService"; Url = "http://localhost:5005/api/notifications/unread-count" },
    @{ Name = "AI-OCR Service"; Url = "http://localhost:5006/health" },
    @{ Name = "EmailWorkerService"; Url = "http://localhost:5007/health" },
    @{ Name = "Gateway"; Url = "http://localhost:8080/health" }
)

foreach ($s in $services) {
    try {
        $res = Invoke-RestMethod -Uri $s.Url -Method Get -TimeoutSec 5
        Write-Host "[OK] $($s.Name) is healthy!" -ForegroundColor Green
    } catch {
        Write-Host "[WARN] $($s.Name) check: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
