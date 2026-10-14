# Password Expiry Notification

Active Directory üzerinde şifresi dolmak üzere olan kullanıcıları tespit edip otomatik e-posta uyarısı gönderen Windows uygulaması.

## Özellikler

- ✅ **AD Entegrasyonu** — Active Directory'den şifre dolma tarihlerini otomatik tespit
- ✅ **Otomatik Uyarı** — SMTP üzerinden e-posta gönderimi
- ✅ **GUI + Service Mode** — GUI arayüzü veya arkaplanda Windows Service olarak çalışma
- ✅ **Zamanlama** — Belirtilen saatte günde 1x otomatik çalışma
- ✅ **Headless CLI** — Task Scheduler ile otomatik tarama/gönderim
- ✅ **Yönetici Özeti** — Gönderilen kullanıcı listesi ile e-posta raporu

## Kullanım

### GUI Mode
```powershell
PasswordExpiryNotification.exe --gui
```

### Service Mode
```powershell
# Kurulum
PasswordExpiryNotification.exe install

# Başlat
net start PasswordExpiryNotification

# Durdur
net stop PasswordExpiryNotification

# Kaldır
PasswordExpiryNotification.exe uninstall
```

### Headless Mode (CLI)
```powershell
# Tarama + Gönderim
PasswordExpiryNotification.exe --scan --send
```

## Teknolojiler

- **.NET 8 WPF** — Windows masaüstü uygulaması
- **Active Directory** — `System.DirectoryServices.AccountManagement`
- **SMTP** — MailKit / MimeKit
- **Topshelf** — Windows Service hosting
- **DPAPI** — Şifre şifreleme (LocalMachine scope)

## Ayarlar

`appsettings.json` dosyasında konfigüre et:

```json
{
  "Smtp": {
    "Server": "your-smtp-server.com",
    "Port": 587,
    "FromAddress": "noreply@company.com",
    "Username": "smtp-username",
    "UseSsl": true
  },
  "System": {
    "OperationMode": "Automatic",
    "ScheduledTime": "06:00",
    "SendAdminSummary": true,
    "AdminSummaryEmail": "admin@company.com"
  }
}
```

## Deployment

### Server 2025'te Kurulum

1. **Release build yap**
   ```powershell
   dotnet publish -c Release
   ```

2. **Dosyaları kopyala**
   ```powershell
   xcopy "bin\Release\net8.0-windows\*" "C:\Program Files\PasswordExpiryNotification" /Y /E
   ```

3. **Service kurulum**
   ```powershell
   cd "C:\Program Files\PasswordExpiryNotification"
   .\PasswordExpiryNotification.exe install
   net start PasswordExpiryNotification
   ```

4. **GUI açarak ayarları konfigüre**
   ```powershell
   .\PasswordExpiryNotification.exe --gui
   ```

Detaylı kurulum rehberi: [DEPLOYMENT-SERVER2025.md](DEPLOYMENT-SERVER2025.md)

## Yapı

```
Services/          SMTP, AD, Logger, Settings, Scheduler
ViewModels/        MVVM ViewModel'ler
Models/            Data modelleri
Logs/              Otomatik log dosyaları
appsettings.json   Konfigürasyon
```

## Loglar

Logs klasöründe tarih bazlı log dosyaları yazılır:
```
Logs/PasswordExpiry_2026-10-13.log
```

## Lisans

MIT

## İletişim

- **Geliştirici:** Mustafa Önder Aköz
- **E-posta:** onder7@gmail.com
- **LinkedIn:** https://www.linkedin.com/in/onderakoz/
