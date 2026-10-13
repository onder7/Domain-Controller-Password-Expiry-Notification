# Yapılanlar

Tarih: 2026-10-09

## Bu oturumda tamamlananlar

### SMTP bağlantı düzeltmesi (Brevo / Sendinblue)
- **Sorun:** MailKit `SecureSocketOptions` port 587 için yanlış seçilebiliyordu (`UseSsl=false` → `Auto`); PowerShell `EnableSsl` ile çalışan Brevo STARTTLS bekliyor.
- **Çözüm:** Port’a göre seçim — `465` → `SslOnConnect`, `587` → `StartTls`, diğer → `UseSsl` / `StartTlsWhenAvailable`.
- Test/gönderim hatalarında `InnerException` zinciri log ve mesaja yazılıyor.
- `appsettings.json` varsayılanları: `smtp-relay.sendinblue.com:587`, From/Username `it@corendon-airlines.com`, `UseSsl: true`; şifre boş (UI’dan girilir, DPAPI ile saklanır).

### Proje iskeleti
- `.NET 8 WPF` projesi oluşturuldu (`PasswordExpiryNotification.csproj`)
- MVVM paketleri: CommunityToolkit.Mvvm, MailKit/MimeKit, DirectoryServices.AccountManagement, ProtectedData (DPAPI), System.Text.Json

### Ayar parametreleri (`appsettings.json` + `pass.ps1` kaynaklı)
| Parametre | Varsayılan |
|-----------|------------|
| SMTP Server / Port / From | `smtp-relay.sendinblue.com` / `587` / `it@corendon-airlines.com` |
| Uyarı günleri (available) | 30, 14, 7, 3, 1 |
| Uyarı günleri (selected) | 14, 7, 3, 1 (pass.ps1 ile aynı) |
| Çalışma modu | Manual |
| Otomatik gönderim | Kapalı |
| Şifre yaşı fallback | 90 gün |
| Şirket adı | Şirketiniz A.Ş. |
| E-posta şablonu | Konu + HTML gövde (düzenlenebilir) |

### Klasör / dosya yapısı
```
Models/          UserModel, SmtpConfigModel, EmailTemplateModel, AdPolicyModel,
                 WarningDayOption, SystemOptionsModel, AppSettingsModel, NotificationHistoryModel
Services/        LogService, SettingsService (+ DPAPI), AdService, EmailService, TemplateRenderer
ViewModels/      MainViewModel, ScanViewModel, PreviewViewModel, SettingsViewModel
MainWindow.xaml  3 sekme: Tarama | Uyarı Önizleme | Ayarlar + canlı log
appsettings.json Ayar dosyası (output’a kopyalanır)
pass.ps1         Mevcut PowerShell (korundu)
README.md        Ürün spesifikasyonu (korundu)
```

### UI özellikleri (iskelet)
1. **Tarama** — AD tara, seçim, CSV export, önizlemeye aktar
2. **Uyarı Önizleme** — alıcı listesi, satır mail önizleme, Onayla ve Gönder / İptal
3. **Ayarlar** — SMTP (+ test), seçilebilir uyarı günleri (+ özel gün), AD politikası, e-posta şablonu (parametre butonları, önizleme, varsayılana dön), Manuel/Otomatik sistem seçenekleri
4. **Hakkında** — Geliştirici: Mustafa Önder Aköz · onder7@gmail.com · [LinkedIn](https://www.linkedin.com/in/onderakoz/)
5. **Canlı log** — ekran + `Logs/PasswordExpiry_yyyyMMdd.log`

### Uygulama simgesi / kısayol
- Kaynak görsel: kök dizindeki `diagram.png`
- Windows `.exe` / kısayol ikonu: `app.ico` (`diagram.png` → çok boyutlu ICO)
- Kullanıldığı yerler: `ApplicationIcon`, pencere başlığı, üst bar logo, Hakkında sekmesi
- ICO yenileme: `powershell -File .\tools\Convert-Icon.ps1`
- Masaüstü kısayolu: `powershell -File .\tools\Create-DesktopShortcut.ps1`

### Servis davranışı
- Ayar kaydı: SMTP şifresi DPAPI ile `PasswordProtected` alanına yazılır
- Tarama: seçili uyarı günlerine göre filtre (`DaysToExpire ∈ SelectedDays`)
- Manuel mod: önizleme onayından sonra gönderim
- Otomatik + AutoSend: tarama sonrası doğrudan gönderim (iskelet akışı bağlı)
- FGPP: arayüz flag’i var; AD hesabında TODO (şimdilik DefaultMaxPasswordAgeDays)

### Bilinen sonraki adımlar
- [ ] FGPP / `msDS-ResultantPSO` gerçek hesabı
- [ ] Tekrar gönderim koruması (notification history dosyası)
- [ ] Yönetici özet maili tam implementasyonu
- [ ] Headless CLI (`--scan --send`)
- [ ] OU / güvenlik grubu filtrelerinin AD sorgusunda uygulanması
- [ ] Zamanlayıcı (SchedulerService)

### Derleme
```
dotnet build   # Başarılı (0 warning, 0 error) — 2026-10-09
dotnet run
```


---

## Bu oturumda tamamlananlar (Otomatik SMTP Kurulumu)

### SMTP Sorunları Çözüldü
1. **DPAPI CurrentUser → LocalMachine**
   - Sorun: Server 2025'te farklı user kontekstinde DPAPI şifresi çözülemiyor
   - Çözüm: `DpapiProtectionService.cs`'de `DataProtectionScope.CurrentUser` → `LocalMachine` değiştirildi
   - Sonuç: Herhangi bir user çalıştırsa şifre decrypt edilebilir ✓

2. **SSL Sertifika Revocation Hatası**
   - Sorun: Server 2025'te revocation server offline → "remote certificate was rejected"
   - Çözüm: `EmailService.cs` `ConnectAsync()`'ye custom certificate validator eklendi
   - Davranış: Revocation-only hataları tolere et, diğer SSL hatalarını reddet
   - Sonuç: SMTP bağlantısı başarılı ✓

### Headless CLI Mode Implement
- **Dosya:** `App.xaml.cs`
- **Komut:** `PasswordExpiryNotification.exe --scan --send`
- **Davranış:** GUI açılmaz, terminal/log'a yazıp çıkış yapar
- **Akış:**
  1. Ayarlar yüklenir
  2. `--scan` → AD taraması (seçili uyarı günlerine göre filtre)
  3. `--send` → E-postalar gönderilir
  4. `--send` + `SendAdminSummary=true` → Yönetici özeti maili

### Windows Task Scheduler Kurulum Scripti
- **Dosya:** `Create-ScheduledTask.ps1`
- **Kullanım:** `.\Create-ScheduledTask.ps1 -ScheduleTime "06:00"` (örn)
- **Sonuç:** Günde 1x belirtilen saatte `--scan --send` mod ile çalışır
- **User:** `SYSTEM` (yüksek izinler, AD erişim)
- **Hatalar:** Restart yapsa da tekrar çalışır

### Deployment Rehberi
- **Dosya:** `DEPLOYMENT-SERVER2025.md`
- **İçerik:**
  1. Uygulamayı Server 2025'e kopyala
  2. Bir kez GUI ile ayarları konfigüre (SMTP, Sistem, Uyarı Günleri)
  3. `Create-ScheduledTask.ps1` çalıştır
  4. Test et, logları takip et
  5. Sorun giderme rehberi

### Değişiklik Özeti
| Dosya | Değişiklik |
|-------|-----------|
| `Services/DpapiProtectionService.cs` | `CurrentUser` → `LocalMachine` |
| `Services/EmailService.cs` | Certificate validator eklendi |
| `App.xaml.cs` | Headless CLI mode implement |
| `Create-ScheduledTask.ps1` | *(Yeni)* — Task Scheduler kurulum |
| `DEPLOYMENT-SERVER2025.md` | *(Yeni)* — Deployment rehberi |

### Bilinen Sonraki Adımlar
- [ ] Tekrar gönderim koruması (notification history) — aynı kullanıcıya aynı gün 2x mail gönderme
- [ ] FGPP / `msDS-ResultantPSO` gerçek hesabı
- [ ] OU / güvenlik grubu filtrelerinin AD sorgusunda uygulanması
- [ ] CLI parametreleri genişletme (`--config`, `--log-level`, vs)
