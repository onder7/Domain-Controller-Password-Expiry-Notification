# Windows Task Scheduler — Günlük otomatik SMTP uyarısı
# Server 2025'te Headless Mode ile çalışacak

param(
    [string]$AppPath = "C:\Program Files\PasswordExpiryNotification\PasswordExpiryNotification.exe",
    [string]$ScheduleTime = "06:00",
    [string]$TaskName = "PasswordExpiryNotification",
    [string]$TaskUser = "SYSTEM"
)

Write-Host "=== Windows Task Scheduler Setup ===" -ForegroundColor Cyan
Write-Host ""

# 1. Task adı ve açıklama
Write-Host "[1] Task oluşturuluyor..." -ForegroundColor Yellow
Write-Host "  Adı: $TaskName" -ForegroundColor Gray
Write-Host "  Zaman: $ScheduleTime (her gün)" -ForegroundColor Gray
Write-Host "  Komut: $AppPath --scan --send" -ForegroundColor Gray
Write-Host ""

# 2. Mevcut task'ı sil (varsa)
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask)
{
    Write-Host "[2] Eski task siliniyor..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "✓ Silindi" -ForegroundColor Green
    Start-Sleep -Seconds 2
}

# 3. Task trigger (günlük, 06:00'da)
Write-Host "[3] Trigger oluşturuluyor..." -ForegroundColor Yellow
$trigger = New-ScheduledTaskTrigger -Daily -At $ScheduleTime
Write-Host "✓ Günlük trigger: $ScheduleTime" -ForegroundColor Green

# 4. Action (çalıştırılacak komut)
$action = New-ScheduledTaskAction `
    -Execute $AppPath `
    -Argument "--scan --send"

# 5. Settings (hata durumunda tekrar denesin, vs)
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable `
    -Compatibility Win8

# 6. Principal (hangi user olarak çalışacak)
$principal = New-ScheduledTaskPrincipal -UserId $TaskUser -RunLevel Highest

# 7. Task'ı kaydet
Write-Host "[4] Task kaydediliyor..." -ForegroundColor Yellow
Register-ScheduledTask `
    -TaskName $TaskName `
    -Trigger $trigger `
    -Action $action `
    -Settings $settings `
    -Principal $principal `
    -Description "Active Directory şifresi dolmak üzere olan kullanıcılara otomatik uyarı e-postası gönderir." `
    -Force

Write-Host "✓ Task kaydedildi!" -ForegroundColor Green

# 8. Doğrulama
Write-Host ""
Write-Host "[5] Task detayları:" -ForegroundColor Yellow
$createdTask = Get-ScheduledTask -TaskName $TaskName
Write-Host "  Durum: $(if($createdTask.State -eq 'Ready') { 'HAZIR ✓' } else { $createdTask.State })" -ForegroundColor Cyan
Write-Host "  Trigger: Günlük $ScheduleTime"
Write-Host "  User: $TaskUser"

# 9. Manuel test (opsiyonel)
Write-Host ""
Write-Host "[6] Manuel Test (opsiyonel):" -ForegroundColor Yellow
Write-Host "  Komut: Start-ScheduledTask -TaskName '$TaskName'" -ForegroundColor Gray
Write-Host "  Loglar: Event Viewer > Windows Logs > Application" -ForegroundColor Gray

Write-Host ""
Write-Host "=== Setup Tamamlandı ===" -ForegroundColor Green
