# Password Expiry Notification — Server 2025 Deployment

Server 2025'te **otomatik arkaplan** SMTP uyarı sistemi kurma adımları.

---

## Adım 1: Uygulamayı Kopyala

**Kaynak:** Kendi bilgisayarından derlenen `bin/Debug/net8.0-windows/`
**Hedef:** Server 2025'te `C:\Program Files\PasswordExpiryNotification\`

```powershell
# Server 2025'te PowerShell (Admin)
New-Item -ItemType Directory -Path "C:\Program Files\PasswordExpiryNotification" -Force

# Kendi bilgisayarından uygulamayı kopyala
Copy-Item `
    -Path "C:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\bin\Debug\net8.0-windows\*" `
    -Destination "C:\Program Files\PasswordExpiryNotification" `
    -Recurse -Force
```

---

## Adım 2: Ayarları Yapılandır (GUI ile)

**Bir kez GUI ile çalıştırıp ayarları kaydet:**

```powershell
C:\Program Files\PasswordExpiryNotification\PasswordExpiryNotification.exe
```

**Ayarlar sekmesinde:**
1. **SMTP:**
   - Server: `smtp-relay.sendinblue.com`
   - Port: `587`
   - Gönderen: `it@corendon-airlines.com`
   - Kullanıcı adı: `it@corendon-airlines.com`
   - Şifre: *(Sendinblue'den kopyala)*
   - SSL/TLS: ✓ (işaretli)
   - **SMTP Testi** yap → Başarılı olmalı

2. **Sistem / Otomasyon:**
   - Çalışma modu: **Automatic** ✓
   - Otomatik tarama: ✓
   - Otomatik gönderim: ✓
   - Yönetici özeti: ✓
   - Zamanlama: `06:00` (istediğin saat)
   - Yönetici maili: `it@corendon-airlines.com`

3. **Uyarı Günleri:** İstediğin günleri seç (örn: 14, 7, 3, 1)

4. **Ayarları Kaydet** (Kaydedildi mesajını gör)

✅ `appsettings.json` şimdi **LocalMachine DPAPI** ile encrypt edilmiştir (herhangi bir user erişebilir).

---

## Adım 3: Windows Task Scheduler Task'ını Kur

Server 2025'te PowerShell (Admin) açarak:

```powershell
cd "C:\Program Files\PasswordExpiryNotification"

# Script'i çalıştır
.\Create-ScheduledTask.ps1 `
    -AppPath "C:\Program Files\PasswordExpiryNotification\PasswordExpiryNotification.exe" `
    -ScheduleTime "06:00" `
    -TaskName "PasswordExpiryNotification" `
    -TaskUser "SYSTEM"
```

**Çıktı örneği:**
```
=== Windows Task Scheduler Setup ===

[1] Task oluşturuluyor...
  Adı: PasswordExpiryNotification
  Zaman: 06:00 (her gün)
  Komut: C:\Program Files\PasswordExpiryNotification\PasswordExpiryNotification.exe --scan --send

[2] Eski task siliniyor...
[3] Trigger oluşturuluyor...
✓ Günlük trigger: 06:00

[4] Task kaydediliyor...
✓ Task kaydedildi!

[5] Task detayları:
  Durum: HAZIR ✓
  Trigger: Günlük 06:00
  User: SYSTEM

=== Setup Tamamlandı ===
```

---

## Adım 4: Manuel Test (Opsiyonel)

Task'ın çalışıp çalışmadığını test et:

```powershell
# PowerShell (Admin)
Start-ScheduledTask -TaskName "PasswordExpiryNotification"

# 30 saniye bekle, sonra log'u kontrol et
Start-Sleep -Seconds 30
Get-Content "C:\Program Files\PasswordExpiryNotification\Logs\PasswordExpiry_*.log" -Tail 20
```

**Başarılı log örneği:**
```
[INFO] === Headless Mode Başladı ===
[INFO] Ayarlar yüklendi.
[INFO] AD taraması başlatılıyor...
[INFO] AD taraması tamamlandı. Eşleşen: 5 (seçili günler: 14,7,3,1)
[INFO] E-postalar gönderiliyor...
[INFO] E-posta gönderildi: user1@domain.com
[INFO] E-posta gönderildi: user2@domain.com
[INFO] Gönderim tamamlandı: Başarılı=2, Hatalı=0
[INFO] === Headless Mode Tamamlandı ===
```

---

## Adım 5: Logları Takip Et

**Task çalıştıktan sonra log dosyasını kontrol et:**

```powershell
# En yeni log dosyasını göster
$latestLog = Get-ChildItem "C:\Program Files\PasswordExpiryNotification\Logs\PasswordExpiry_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $latestLog.FullName -Tail 50
```

**Windows Event Viewer'da da görebilirsin:**
- Event Viewer → Windows Logs → Application
- Source: `.NET Runtime` veya uygulama adı
- Hata varsa orada görünecek

---

## Sorun Giderme

### Task çalışmıyor
```powershell
# Task durumunu kontrol et
Get-ScheduledTask -TaskName "PasswordExpiryNotification" | Get-ScheduledTaskInfo

# Son çalışma sonucunu göster
(Get-ScheduledTask -TaskName "PasswordExpiryNotification" | Get-ScheduledTaskInfo).LastTaskResult
# 0 = Başarı
# 1 = Hata
```

### SMTP bağlantı hatası
1. `appsettings.json` şifresi doğru mu? → GUI'den yeniden gir
2. Server 2025 DNS çözümlüyor mu?
   ```powershell
   nslookup smtp-relay.sendinblue.com
   ```
3. Firewall outbound 587 açık mı?

### AD taraması başarısız
1. Server 2025 domain'e katılı mı?
2. `SYSTEM` user'ı AD'ye erişebiliyor mu?
   ```powershell
   whoami
   nltest /dsgetdc:corendon.local
   ```

---

## Yönetim Komutları

```powershell
# Task'ı başlat
Start-ScheduledTask -TaskName "PasswordExpiryNotification"

# Task'ı durdur
Stop-ScheduledTask -TaskName "PasswordExpiryNotification"

# Task'ı devre dışı bırak
Disable-ScheduledTask -TaskName "PasswordExpiryNotification"

# Task'ı tekrar etkinleştir
Enable-ScheduledTask -TaskName "PasswordExpiryNotification"

# Task'ı sil
Unregister-ScheduledTask -TaskName "PasswordExpiryNotification" -Confirm:$false

# Task detaylarını göster
Get-ScheduledTask -TaskName "PasswordExpiryNotification" | Select-Object -ExpandProperty Triggers
```

---

## Notlar

- **DPAPI LocalMachine:** Şifre `LocalMachine` DPAPI ile encrypt ediliyor, herhangi bir user erişebilir
- **Headless Mode:** `--scan --send` flag'leri ile GUI açılmaz, terminal/log'a yazar
- **Tekrar gönderim koruması:** *(Bilinen TODO — henüz implement edilmedi)*
- **Yönetici özeti:** Her çalışmadan sonra özet mail gönderilir (ayarlardan etkinleştir)

---

## Geri Alma

Kurulumu geri almak için:

```powershell
# Task'ı sil
Unregister-ScheduledTask -TaskName "PasswordExpiryNotification" -Confirm:$false

# Uygulaması sil (opsiyonel)
Remove-Item -Path "C:\Program Files\PasswordExpiryNotification" -Recurse -Force
```
