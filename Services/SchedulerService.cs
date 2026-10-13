using System.Timers;
using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

/// <summary>
/// Otomatik AD taraması ve e-posta gönderimi zamanlama servisi.
/// GUI'de Automatic modda, belirtilen saatte günde 1x çalışır.
/// </summary>
public sealed class SchedulerService : ISchedulerService, IDisposable
{
    private readonly IAdService _adService;
    private readonly IEmailService _emailService;
    private readonly ILogService _log;

    private System.Timers.Timer? _timer;
    private AppSettingsModel? _currentSettings;
    private DateTime _lastRun = DateTime.MinValue;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public event EventHandler<SchedulerScanCompletedEventArgs>? OnScanCompleted;
    public event EventHandler<SchedulerSendCompletedEventArgs>? OnSendCompleted;
    public event EventHandler<SchedulerErrorEventArgs>? OnError;

    public SchedulerService(IAdService adService, IEmailService emailService, ILogService log)
    {
        _adService = adService;
        _emailService = emailService;
        _log = log;
    }

    public void Start(AppSettingsModel settings)
    {
        if (_isRunning)
            Stop();

        _currentSettings = settings;

        // Manual modda timer çalışmaz
        if (settings.System.OperationMode != OperationMode.Automatic)
        {
            _log.Info("Çalışma modu Manual — Otomatik zamanlama devre dışı.");
            return;
        }

        // Otomatik tarama/gönderim etkin değilse çalışmaz
        if (!settings.System.AutoScanEnabled && !settings.System.AutoSendEnabled)
        {
            _log.Info("Otomatik tarama ve gönderim kapalı — Zamanlama devre dışı.");
            return;
        }

        // Timer'ı başlat
        _timer = new System.Timers.Timer();
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = false; // Her çalışmadan sonra tekrar aralığı ayarla
        ScheduleNextRun(settings);

        _isRunning = true;
        _log.Info($"Otomatik zamanlama başladı. Sonraki çalışma: {GetNextRunTime(settings)}");
    }

    public void Stop()
    {
        if (_timer == null)
            return;

        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        _isRunning = false;

        _log.Info("Otomatik zamanlama durduruldu.");
    }

    private void ScheduleNextRun(AppSettingsModel settings)
    {
        if (_timer == null)
            return;

        var nextRun = GetNextRunTime(settings);
        var now = DateTime.Now;
        var timeToWait = nextRun - now;

        if (timeToWait.TotalMilliseconds < 0)
            timeToWait = timeToWait.Add(TimeSpan.FromDays(1)); // Yarın aynı saatte

        _timer.Interval = timeToWait.TotalMilliseconds;
        _timer.Start();

        _log.Info($"Timer kuruldu. Sonraki çalışma: {timeToWait.Hours}s {timeToWait.Minutes}d {timeToWait.Seconds}s sonra ({nextRun:HH:mm:ss})");
    }

    private DateTime GetNextRunTime(AppSettingsModel settings)
    {
        if (!TimeSpan.TryParse(settings.System.ScheduledTime, out var scheduledTime))
            scheduledTime = TimeSpan.FromHours(6); // Default 06:00

        var today = DateTime.Today.Add(scheduledTime);
        if (today > DateTime.Now)
            return today;

        return today.AddDays(1); // Yarın aynı saatte
    }

    private async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            if (_currentSettings == null)
                return;

            // Gün değişti mi kontrol et (günde 1x koruması)
            if (_lastRun.Date == DateTime.Today)
            {
                _log.Warning("Bugün zaten çalışmış — Tekrar çalışmıyor.");
                ScheduleNextRun(_currentSettings);
                return;
            }

            _log.Info("=== Otomatik Zamanlama Çalışması Başladı ===");
            _lastRun = DateTime.Now;

            // Tarama
            if (_currentSettings.System.AutoScanEnabled)
            {
                _log.Info("Otomatik AD taraması başlatılıyor...");
                var users = await _adService.ScanExpiringUsersAsync(_currentSettings);
                _log.Info($"AD taraması tamamlandı: {users.Count} kullanıcı");

                OnScanCompleted?.Invoke(this, new SchedulerScanCompletedEventArgs
                {
                    Users = users,
                    CompletedAt = DateTime.Now
                });

                // Gönderim
                if (_currentSettings.System.AutoSendEnabled && users.Any())
                {
                    _log.Info("Otomatik e-posta gönderimi başlatılıyor...");
                    var (successCount, failCount) = await _emailService.SendWarningsAsync(_currentSettings, users);
                    _log.Info($"Gönderim tamamlandı: Başarılı={successCount}, Hatalı={failCount}");

                    // Yönetici özeti
                    if (_currentSettings.System.SendAdminSummary && !string.IsNullOrWhiteSpace(_currentSettings.System.AdminSummaryEmail))
                    {
                        var adminSubject = $"[Özet] Otomatik Şifre Dolma Uyarısı - {DateTime.Now:yyyy-MM-dd HH:mm}";
                        
                        // Kullanıcı listesi HTML tablosu
                        var userListHtml = GenerateUserListHtml(users);
                        
                        var adminBody = $@"<html><body style=""font-family: Arial, sans-serif; color: #333; line-height: 1.6;"">
<h2>Şifre Dolma Uyarısı — Otomatik Gönderim Özeti</h2>

<div style=""background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;"">
  <p><strong>Özet İstatistikler:</strong></p>
  <ul>
    <li><strong>Başarılı:</strong> {successCount}</li>
    <li><strong>Hatalı:</strong> {failCount}</li>
    <li><strong>Toplam Kullanıcı:</strong> {users.Count}</li>
    <li><strong>Tarih/Saat:</strong> {DateTime.Now:G}</li>
    <li><strong>Çalışma Modu:</strong> Otomatik (GUI açık)</li>
  </ul>
</div>

<h3>Uyarı Gönderilen Kullanıcılar:</h3>
{userListHtml}

<p style=""color: #666; font-size: 12px; margin-top: 30px;"">
  <em>Bu e-posta otomatik olarak gönderilmiştir. Lütfen yanıt vermeyiniz.</em>
</p>
</body></html>";
                        await _emailService.SendAsync(_currentSettings.Smtp, _currentSettings.System.AdminSummaryEmail, adminSubject, adminBody);
                    }

                    OnSendCompleted?.Invoke(this, new SchedulerSendCompletedEventArgs
                    {
                        SuccessCount = successCount,
                        FailCount = failCount,
                        CompletedAt = DateTime.Now
                    });
                }
            }

            _log.Info("=== Otomatik Zamanlama Çalışması Tamamlandı ===");
        }
        catch (Exception ex)
        {
            _log.Error($"Otomatik zamanlama hatası: {ex.Message}");
            OnError?.Invoke(this, new SchedulerErrorEventArgs
            {
                Message = ex.Message,
                Exception = ex,
                OccurredAt = DateTime.Now
            });
        }
        finally
        {
            // Bir sonraki çalışma için timer'ı yeniden kur
            if (_currentSettings != null)
                ScheduleNextRun(_currentSettings);
        }
    }

    public void Dispose()
    {
        Stop();
        _timer?.Dispose();
    }

    private string GenerateUserListHtml(IReadOnlyList<UserModel> users)
    {
        if (!users.Any())
            return "<p><em>Kullanıcı bulunamadı.</em></p>";

        var html = @"<table style=""width: 100%; border-collapse: collapse; margin: 20px 0;"">
  <thead>
    <tr style=""background-color: #4CAF50; color: white; text-align: left;"">
      <th style=""padding: 12px; border: 1px solid #ddd;"">Adı Soyadı</th>
      <th style=""padding: 12px; border: 1px solid #ddd;"">Kullanıcı Adı</th>
      <th style=""padding: 12px; border: 1px solid #ddd;"">E-posta</th>
      <th style=""padding: 12px; border: 1px solid #ddd;"">Kalan Gün</th>
      <th style=""padding: 12px; border: 1px solid #ddd;"">Şifre Dolma</th>
    </tr>
  </thead>
  <tbody>";

        for (int i = 0; i < users.Count; i++)
        {
            var user = users[i];
            var rowColor = i % 2 == 0 ? "#f9f9f9" : "#fff";
            html += $@"
    <tr style=""background-color: {rowColor};"">
      <td style=""padding: 10px; border: 1px solid #ddd;"">{user.DisplayName}</td>
      <td style=""padding: 10px; border: 1px solid #ddd;"">{user.UserName}</td>
      <td style=""padding: 10px; border: 1px solid #ddd;"">{user.Email}</td>
      <td style=""padding: 10px; border: 1px solid #ddd; text-align: center;""><strong>{user.DaysToExpire}</strong></td>
      <td style=""padding: 10px; border: 1px solid #ddd;"">{user.PasswordExpiryDate:dd.MM.yyyy}</td>
    </tr>";
        }

        html += @"
  </tbody>
</table>";

        return html;
    }
}
