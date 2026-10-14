# Server 2025 Deploy Script
# Adımlar:
# 1. Güncellenmiş app'i kopyala
# 2. SMTP şifresini yeniden kaydet (LocalMachine DPAPI ile)

Write-Host "=== Password Expiry Notification - Server 2025 Deploy ===" -ForegroundColor Cyan

# 1. Yeni DLL'i kopyala (gerek varsa)
Write-Host ""
Write-Host "[1] Uygulamayı kopyala..." -ForegroundColor Yellow
# $sourceDir = "C:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\bin\Debug\net8.0-windows"
# $destDir = "C:\Program Files\PasswordExpiryNotification"
# Copy-Item "$sourceDir\*" $destDir -Recurse -Force
# Write-Host "✓ Kopyalandı: $destDir" -ForegroundColor Green

# 2. Uygulamayı başlat
Write-Host ""
Write-Host "[2] Uygulamayı başlat..." -ForegroundColor Yellow
Write-Host "Şu yolu çalıştır:" -ForegroundColor Cyan
Write-Host "  C:\path\to\PasswordExpiryNotification.exe" -ForegroundColor White

Write-Host ""
Write-Host "[3] Ayarlar'dan SMTP şifresini GİR (boş alan):" -ForegroundColor Yellow
Write-Host "  - Ayarlar sekmesine git" -ForegroundColor Gray
Write-Host "  - SMTP bölümüne scroll et" -ForegroundColor Gray
Write-Host "  - Şifre alanına kurumsal SMTP şifrenizi kopyala" -ForegroundColor Gray
Write-Host "  - SMTP Testi yap" -ForegroundColor Gray
Write-Host "  - Kaydedildi → LocalMachine DPAPI ile encrypt edilir" -ForegroundColor Gray

Write-Host ""
Write-Host "=== Deploy Hazır ===" -ForegroundColor Cyan
