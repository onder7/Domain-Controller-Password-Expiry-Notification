# Password Expiry Notification

Active Directory üzerinde şifresi dolmak üzere olan kullanıcıları tespit edip e-posta uyarısı gönderen Windows Masaüstü Uygulaması (WPF + MVVM). Mevcut PowerShell script mantığının modern C# mimarisine dönüştürülmesi.

---

## Teknolojiler

| Alan | Seçim |
|------|--------|
| Framework | .NET 8.0 (WPF) |
| Mimari | MVVM (Model-View-ViewModel) |
| Active Directory | `System.DirectoryServices.AccountManagement` |
| SMTP | MailKit / MimeKit |
| Ayarlar | `appsettings.json` (+ DPAPI ile şifreli SMTP kimlik bilgisi) |

---

## Arayüz (UI / XAML)

### 1. Konfigürasyon sekmesi

**SMTP ayarları**
- Sunucu adresi, Port, Gönderen e-posta
- Kullanıcı adı / şifre, SSL/TLS
- SMTP bağlantı testi
- "Kendime test maili gönder" (şablonun gerçek render hali)

**AD politikası ve filtreler**
- Varsayılan şifre yaşı (örn. 90 gün) — FGPP yoksa fallback
- **Uyarı günleri (seçilebilir — zorunlu)**
  - Uygulamada hazır seçenekler sunulur (Checkbox list / multi-select); serbest metin zorunlu değil
  - Varsayılan seçenekler: `30`, `14`, `7`, `3`, `1` (gün)
  - Kullanıcı istediği günleri işaretler; yalnızca işaretli eşikler için bildirim yapılır
  - Özel gün ekleme: isteğe bağlı sayı ekle (örn. `10`) → listeye dahil edilir ve seçilebilir olur
  - En az bir gün seçili olmalı (ayar kaydı / tarama öncesi doğrulama)
  - Örnek: sadece `7` ve `1` seçiliyse → kalan günü 7 veya 1 olan kullanıcılara mail gider; 14 veya 3 olanlara gitmez
  - Seçim `appsettings.json`'a kaydedilir; açılışta geri yüklenir
- Filtreler: OU, güvenlik grubu; disabled / `PasswordNeverExpires` / e-postası boş kullanıcıları hariç tut

**E-posta şablonu (uygulama içinden düzenlenebilir — zorunlu)**
- Mail konusu ve HTML gövdesi kodda sabitlenmez; Konfigürasyon sekmesinden tamamen düzenlenir
- Konu alanı: tek satır TextBox
- Gövde alanı: çok satırlı HTML düzenleyici (TextBox / RichTextBox)
- Parametre ekleme butonları (tıklayınca imleç konumuna `{Name}` vb. ekler)
- Desteklenen parametreler: `{Name}`, `{UserName}`, `{DaysToExpire}`, `{ExpiryDate}`, `{LastPasswordSet}`, `{CompanyName}`
- **Kaydet** → `appsettings.json`'a yazılır; sonraki açılışta otomatik yüklenir
- **Varsayılana dön** → aşağıdaki örnek şablonu geri yükler
- **Önizleme** → örnek kullanıcıyla konu + gövde render (mail atmadan)
- **Kendime test maili gönder** → kaydedilmiş şablonun gerçek SMTP ile testi

**Güvenlik**
- SMTP şifresi düz metin saklanmaz; Windows DPAPI ile şifrelenir

### 2. Kullanıcı / Tarama sekmesi

- **AD'yi Tara** — async/await, ProgressBar + durum metni
- DataGrid kolonları: Seç, Kullanıcı Adı, E-posta, Şifre Son Değişim, Şifre Dolma, Kalan Gün, OU (opsiyonel)
- CSV / Excel export
- Tarama sonrası: uyarı gönderilecekler **Uyarı Önizleme** ekranına aktarılır

### 3. Uyarı Önizleme ekranı (ayrı sekme / sayfa — zorunlu)

Uyarı maili **gönderilmeden önce** alıcıları bu ayrı ekranda görmek zorunludur (manuel modda).

- Başlık: kaç kullanıcıya mail gideceği özeti (örn. `14 kullanıcıya uyarı hazır`)
- DataGrid: Kullanıcı Adı, E-posta, Kalan Gün, Dolma Tarihi, Eşleşen Uyarı Günü (seçili eşiklerden hangisi), Şablon Önizleme (satırda veya detay)
- Liste yalnızca Konfigürasyonda **seçili uyarı günleriyle** eşleşen kullanıcıları gösterir
- Seç / seçimi kaldır; istenmeyen alıcıyı listeden çıkar
- Seçili satır için **mail önizlemesi** (konu + HTML gövde, parametreler doldurulmuş)
- Butonlar:
  - **Onayla ve Gönder** — yalnızca bu ekrandan onay sonrası gerçek SMTP gönderimi (manuel mod)
  - **İptal / Tarama ekranına dön**
- İlerleme: `Gönderilen: 3/15` + canlı durum
- Otomatik gönderim açıkken: bu ekran yine son gönderilen / planlanan listeyi audit için gösterir (salt okunur veya son koşu raporu)

### 4. Sistem / Otomasyon seçenekleri (Konfigürasyon içinde)

Uygulama **hem manuel hem otomatik** çalışır; varsayılan **manuel** (önce önizleme, sonra onay).

| Seçenek | Açıklama |
|---------|----------|
| Çalışma modu | `Manuel` / `Otomatik` |
| Otomatik tarama | Açıkken AD taraması zamanlanmış veya açılışta çalışır |
| Otomatik gönderim | Açıkken uyarı mailleri önizleme onayı beklemeden gönderilir |
| Zamanlama | Günlük saat (örn. 08:00) veya Task Scheduler + `--scan --send` |
| Yönetici özeti | Otomatik koşu bitince IT'ye özet mail |

**Kurallar**
- `Otomatik gönderim = Kapalı` → Tarama → **Uyarı Önizleme ekranı** → kullanıcı onaylar → gönder
- `Otomatik gönderim = Açık` → Tarama → filtre / tekrar gönderim koruması → doğrudan gönder (önizleme ekranı rapor amaçlı)
- Otomatik gönderim açılırken net uyarı: *"Mailler onay beklemeden gönderilecek"*

### 5. Canlı log

- Zaman damgalı, renkli log alanı
- Aynı anda yerel `.log` dosyasına yazım

---

## Backend

### Active Directory
- `PrincipalSearcher` + `UserPrincipal` ile aktif, `PasswordNeverExpires = false` kullanıcılar (`Task.Run` / async)
- Şifre dolma hesabı:
  1. Mümkünse Fine-Grained Password Policy (`msDS-ResultantPSO`) max password age
  2. Yoksa domain varsayılan / yapılandırılan şifre yaşı
  3. `LastPasswordSet` + max age → dolma tarihi ve kalan gün
- **Uyarı eşleştirme:** `DaysToExpire` ∈ seçili uyarı günleri listesi ise kullanıcı bildirim adayıdır (örn. seçim `[14,7,1]` → kalan gün 14, 7 veya 1 olanlar)

### E-posta
- Asenkron SMTP; hata durumunda uygulama kilitlenmez (try/catch + detaylı log)
- SMTP rate limit (toplu gönderimde kısa gecikme)
- Tekrar gönderim koruması: aynı kullanıcıya aynı gün / aynı eşik için ikinci mail yok (yerel geçmiş)
- İşlem bitince yöneticiye özet mail (X başarılı, Y hata)

### Ayarlar ve otomasyon
- Açılışta `appsettings.json` yükleme
- Sistem seçeneği: `OperationMode` = `Manual` | `Automatic`
- `Automatic` + zamanlama veya headless: `--scan --send` (Task Scheduler)
- Manuel modda gönderim yalnızca Uyarı Önizleme ekranındaki onay sonrası tetiklenir
- Ayar doğrulama: tara / gönder öncesi zorunlu alan kontrolü

---

## Varsayılan e-posta içeriği

**Önemli:** Konu ve gövde yalnızca uygulama arayüzünden (Konfigürasyon → E-posta Şablonu) yönetilir. Aşağıdaki metin ilk kurulum / "Varsayılana dön" için örnek şablondur; kullanıcı istediği gibi değiştirip kaydeder.

### Konu

```
[{CompanyName}] Şifreniz {DaysToExpire} gün içinde sona erecek
```

### HTML gövde (varsayılan)

```html
<html>
<body style="font-family: Segoe UI, Arial, sans-serif; color: #222; line-height: 1.5;">
  <p>Sayın <strong>{Name}</strong>,</p>

  <p>
    Kurumsal hesap şifrenizin (<strong>{UserName}</strong>)
    <strong>{DaysToExpire} gün</strong> içinde
    (<strong>{ExpiryDate}</strong> tarihinde) süresi dolacaktır.
  </p>

  <p>
    Erişiminizin kesilmemesi için lütfen şifrenizi süresi dolmadan yenileyiniz.
  </p>

  <p><strong>Nasıl değiştirebilirsiniz?</strong></p>
  <ul>
    <li>Kurumsal bilgisayarınızda Ctrl + Alt + Delete → <em>Şifreyi Değiştir</em></li>
    <li>veya şirket şifre değiştirme portalınız üzerinden</li>
  </ul>

  <p>
    Son şifre değişim tarihiniz: <strong>{LastPasswordSet}</strong>
  </p>

  <p>
    Bu mesaj otomatik olarak gönderilmiştir. Yardım için IT Destek ile iletişime geçiniz.
  </p>

  <p style="color:#666; font-size:12px;">
    {CompanyName} — Bilgi Teknolojileri
  </p>
</body>
</html>
```

### Parametreler

| Parametre | Açıklama | Örnek |
|-----------|----------|--------|
| `{Name}` | Görünen ad (DisplayName) | Ahmet Yılmaz |
| `{UserName}` | SAM hesap adı | ahmet.yilmaz |
| `{DaysToExpire}` | Kalan gün | 7 |
| `{ExpiryDate}` | Şifre dolma tarihi | 16.10.2026 |
| `{LastPasswordSet}` | Son şifre değişimi | 18.07.2026 |
| `{CompanyName}` | Kurum adı (ayarlar) | Örnek A.Ş. |

### Örnek rendered konu

```
[Örnek A.Ş.] Şifreniz 7 gün içinde sona erecek
```

---

## Öncelikli özellikler (ilk sprint)

1. Ayrı **Uyarı Önizleme** ekranı (gönderim öncesi alıcı listesi + onay)
2. Seçilebilir uyarı günleri → tarama ve bildirim bu seçime göre
3. Sistem seçeneği: Manuel / Otomatik gönderim
4. Uygulama içi e-posta şablonu düzenleme + kaydet / önizleme / test maili
5. SMTP DPAPI + FGPP + tekrar gönderim koruması

---

## Geliştirme adımları (kod sırası)

1. Model sınıfları (`UserModel`, `SmtpConfigModel`, `AppSettingsModel`, `NotificationHistoryModel`)
2. Servisler (`AdService`, `EmailService`, `SettingsService`, `NotificationHistoryService`, `LogService`, `SchedulerService`)
3. ViewModel'ler (`MainViewModel`, `ScanViewModel`, `PreviewViewModel`, `SettingsViewModel`) + `RelayCommand` / `AsyncRelayCommand`
4. XAML: `MainWindow` + sekmeler (`ScanView`, `PreviewView`, `SettingsView`) — temiz, kurumsal margin/padding
5. Birim testleri — şifre dolma hesabı + manuel/otomatik gönderim akışı

---

## Akış özeti

```
[Manuel]   Uyarı günlerini seç → AD Tara → (seçili günlere göre filtre) → Önizleme → Onayla ve Gönder → SMTP
[Otomatik] Uyarı günlerini seç → AD Tara → (seçili günler + tekrar koruması) → Doğrudan SMTP → rapor + yönetici özeti
```

Uyarı günleri uygulamadan seçilir; bildirim yalnızca seçilen eşiklere göre yapılır. Mail konusu/içeriği uygulamadan düzenlenir. Ayarlar kalıcı; SMTP DPAPI ile güvenli.
