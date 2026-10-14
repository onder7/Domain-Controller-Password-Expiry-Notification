# Active Directory Şifre Uyarı Sistemi 
# Bu script, şifresi yakın zamanda dolacak kullanıcılara otomatik e-posta gönderir

# AD Modülünü içe aktarma
Import-Module ActiveDirectory 

# Dizin ve Log Yolları
$ScriptDir = "C:\Scripts\PasswordNotification"
$LogFile   = "$ScriptDir\PasswordExpiryLog_$(Get-Date -Format 'yyyyMMdd').log"

# Script klasörü kontrolü
if (-not (Test-Path $ScriptDir)) { 
    New-Item -ItemType Directory -Path $ScriptDir -Force | Out-Null
}

# Loglama fonksiyonu
function Write-Log {
    param( [string]$Message )
    $TimeStamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$TimeStamp - $Message" | Out-File -FilePath $LogFile -Append -Encoding utf8
}

Write-Log "Script başlatıldı."

# E-posta ayarları
$smtpServer    = "your-smtp-server.com"
$smtpPort      = 587
$smtpFrom      = "noreply@company.com"
$emailSubject  = "Önemli Bildirim: Şifreniz yakında sona erecek"
$emailSignature = @"
<p>Saygılarımızla,<br>
IT Departmanı<br>
<strong>Şirketiniz A.Ş.</strong><br>
Tel: +90 212 555 55 55</p>
"@

# Şifre politikası bilgilerini al
try {
    $passwordPolicy = Get-ADDefaultDomainPasswordPolicy
    $maxPasswordAge = $passwordPolicy.MaxPasswordAge.Days
    Write-Log "Şifre politikası bilgisi alındı. Maksimum şifre yaşı: $maxPasswordAge gün."
} catch {
    Write-Log "Hata: Şifre politikası alınamadı: $_"
    exit
}

# Uyarı yapılacak gün aralıkları
$warningDays = @(14, 7, 3, 1)

# Bugünün tarihini saat/dakikasız al (gece yarısı yuvarlama)
$today = (Get-Date).Date

# Aktif ve şifresi süresiz olmayan kullanıcıları al
try {
    $users = Get-ADUser -Filter {Enabled -eq $true -and PasswordNeverExpires -eq $false} -Properties Name, EmailAddress, PasswordLastSet
    Write-Log "Active Directory'den $($users.Count) aktif kullanıcı alındı."
} catch {
    Write-Log "Hata: Active Directory'den kullanıcılar alınamadı: $_"
    exit
}

# Her bir kullanıcı için kontrol et
foreach ($user in $users) {
    # Kullanıcının e-posta adresi yoksa atla
    if ([string]::IsNullOrEmpty($user.EmailAddress)) {
        Write-Log "Kullanıcı $($user.Name) için e-posta adresi bulunamadı, atlanıyor."
        continue
    }

    # Şifre son değiştirilme tarihi yoksa atla
    if (-not $user.PasswordLastSet) {
        Write-Log "Kullanıcı $($user.Name) için şifre değişiklik tarihi bulunamadı, atlanıyor."
        continue
    }

    # Şifre dolma tarihini hesapla
    $passwordSetDate = $user.PasswordLastSet.Date
    $expireDate      = $passwordSetDate.AddDays($maxPasswordAge)
    $daysToExpire    = ($expireDate - $today).Days

    # Belirlenen uyarı günlerinden birinde ise e-posta gönder
    if ($warningDays -contains $daysToExpire) {
        Write-Log "Kullanıcı $($user.Name) için şifre $daysToExpire gün içinde dolacak. Bildirim hazırlanıyor..."

        $emailBody = @"
<html>
<head>
<style>
    body { font-family: Arial, sans-serif; color: #333; }
    .header { background-color: #0056b3; color: white; padding: 10px; }
    .content { padding: 20px; }
    .footer { font-size: 12px; color: #666; border-top: 1px solid #eee; padding-top: 10px; }
    .highlight { color: #d9534f; font-weight: bold; }
</style>
</head>
<body>
    <div class="header">
        <h2>Şifre Değiştirme Hatırlatması</h2>
    </div>
    <div class="content">
        <p>Sayın $($user.Name),</p>
        <p>Bu e-posta, şirket hesabınızın şifresinin <span class="highlight">$daysToExpire gün içinde</span> sona ereceğini bildirmek için gönderilmiştir.</p>
        <p>Lütfen, erişim kesintisi yaşamamak için şifrenizi en kısa sürede değiştirin.</p>
        <h3>Şifre Değiştirme İşlemi:</h3>
        <ol>
            <li>Windows bilgisayarınızda <strong>CTRL+ALT+DEL</strong> tuş kombinasyonunu kullanın</li>
            <li>"Şifre Değiştir" seçeneğini tıklayın</li>
            <li>Mevcut şifrenizi ve yeni şifrenizi girin</li>
        </ol>
        <p><strong>Şifre Politikası Hatırlatması:</strong></p>
        <ul>
            <li>En az 8 karakter uzunluğunda olmalı</li>
            <li>Büyük harf, küçük harf, rakam ve özel karakter içermeli</li>
            <li>Son 5 şifrenizden farklı olmalı</li>
        </ul>
        <p>Herhangi bir sorunuz veya yardıma ihtiyacınız olursa, lütfen IT Yardım Masası ile iletişime geçin.</p>
    </div>
    <div class="footer">
        $emailSignature
    </div>
</body>
</html>
"@

        try {
            # E-posta gönderme
            $smtp = New-Object Net.Mail.SmtpClient($smtpServer, $smtpPort)
            $msg  = New-Object Net.Mail.MailMessage
            $msg.From = $smtpFrom
            $msg.To.Add($user.EmailAddress)
            $msg.Subject = $emailSubject
            $msg.BodyEncoding = [System.Text.Encoding]::UTF8
            $msg.Body = $emailBody
            $msg.IsBodyHtml = $true
            
            $smtp.Send($msg)
            Write-Log "E-posta başarıyla gönderildi: $($user.EmailAddress)"
        } catch {
            Write-Log "E-posta gönderimi başarısız: $($user.EmailAddress), Hata: $_"
        }
    }
}

Write-Log "Script tamamlandı."
Write-Host "Şifre uyarı gönderimi tamamlandı. Lütfen logları kontrol edin: $LogFile" -ForegroundColor Green