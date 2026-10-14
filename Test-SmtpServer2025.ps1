# Server 2025 SMTP bağlantı testi
# Bu scripti Server 2025'te çalıştır

Write-Host "=== SMTP Connection Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# 1. DNS Resolution Test
Write-Host "[1] DNS Çözümleme Testi" -ForegroundColor Yellow
try {
    $dns = [System.Net.Dns]::GetHostAddresses("your-smtp-server.com")
    Write-Host "✓ DNS Çözüldü: $($dns[0].IPAddressToString)" -ForegroundColor Green
} catch {
    Write-Host "✗ DNS Çözümleme Başarısız: $_" -ForegroundColor Red
}

# 2. Port Bağlantı Testi (Network Connectivity)
Write-Host ""
Write-Host "[2] Port 587 Bağlantı Testi" -ForegroundColor Yellow
$tcpClient = New-Object System.Net.Sockets.TcpClient
try {
    $tcpClient.Connect("your-smtp-server.com", 587)
    Write-Host "✓ Port 587 Açık ve Erişilebilir" -ForegroundColor Green
    $tcpClient.Close()
} catch {
    Write-Host "✗ Port 587 Bağlantısı Başarısız: $_" -ForegroundColor Red
}

# 3. SMTP İlk El Handshake (Raw SMTP)
Write-Host ""
Write-Host "[3] SMTP Handshake Testi" -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("your-smtp-server.com", 587)
    $stream = $tcpClient.GetStream()
    $reader = New-Object System.IO.StreamReader($stream)
    
    $banner = $reader.ReadLine()
    Write-Host "✓ SMTP Sunucundan: $banner" -ForegroundColor Green
    
    $tcpClient.Close()
} catch {
    Write-Host "✗ SMTP Handshake Başarısız: $_" -ForegroundColor Red
}

# 4. PowerShell SmtpClient Test (New-Object Net.Mail.SmtpClient)
Write-Host ""
Write-Host "[4] PowerShell Net.Mail.SmtpClient Test (Uygulamanın benzeri)" -ForegroundColor Yellow
try {
    $smtpClient = New-Object Net.Mail.SmtpClient("your-smtp-server.com", 587)
    $smtpClient.EnableSsl = $true
    $smtpClient.Timeout = 5000
    
    # Credentials (appsettings.json'dan)
    $cred = New-Object System.Net.NetworkCredential
    $cred.UserName = "smtp-username"
    $cred.Password = ""  # ← appsettings.json'dan password ekle
    $smtpClient.Credentials = $cred
    
    Write-Host "Bağlanıyor..." -ForegroundColor Cyan
    $smtpClient.Send("noreply@company.com", "it@company.com", "Test Subject", "Test Body")
    Write-Host "✓ E-posta gönderimi başarılı!" -ForegroundColor Green
} catch {
    Write-Host "✗ E-posta gönderilemedi: $_" -ForegroundColor Red
    Write-Host "Inner Exception: $($_.Exception.InnerException)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Tamamlandı ===" -ForegroundColor Cyan
