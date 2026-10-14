using System.Windows;
using PasswordExpiryNotification.Models;
using PasswordExpiryNotification.Services;
using PasswordExpiryNotification.ViewModels;

namespace PasswordExpiryNotification;

public partial class App : Application
{
    public static string[] StartupArgs { get; set; } = Array.Empty<string>();

    public App()
    {
        // MainWindow is created explicitly in OnStartup after services are initialized.
    }

    private ISchedulerService? _schedulerService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Use static StartupArgs passed from Program.cs
        var args = StartupArgs.Length > 0 ? StartupArgs : e.Args.Cast<string>().ToArray();

        var log = new LogService();
        var settingsService = new SettingsService(log);
        var adService = new AdService(log);
        var emailService = new EmailService(log);
        var settings = settingsService.Load();
        var schedulerService = new SchedulerService(adService, emailService, log);
        _schedulerService = schedulerService;

        // Headless mode kontrolü
        if (HasHeadlessArgs(args))
        {
            // CLI mode — GUI açma
            var exitCode = await RunHeadlessMode(log, settingsService, adService, emailService, args);
            Environment.ExitCode = exitCode;
            Shutdown(exitCode);
            return;
        }

        // Defensive check: if no special args and we're in service context, exit immediately
        if (!args.Contains("--gui") && !args.Contains("--scan") && 
            !args.Contains("--send") && !args.Contains("--headless"))
        {
            // Service mode reached app by accident — should not happen
            log.Warning("Uygulamaya beklenmeyen başlatma: GUI başlatılmıyor.");
            Shutdown(0);
            return;
        }

        // Normal GUI mode
        var mainVm = new MainViewModel(settingsService, adService, emailService, log, schedulerService);
        var window = new MainWindow(mainVm);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _schedulerService?.Stop();
        base.OnExit(e);
    }

    private static bool HasHeadlessArgs(string[] args) =>
        args.Contains("--scan") || args.Contains("--send") || args.Contains("--headless");

    private static async Task<int> RunHeadlessMode(
        ILogService log,
        ISettingsService settings,
        IAdService ad,
        IEmailService email,
        string[] args)
    {
        try
        {
            log.Info("=== Headless Mode Başladı ===");
            var appSettings = settings.Load();

            if (args.Contains("--scan"))
            {
                log.Info("AD taraması başlatılıyor...");
                var users = await ad.ScanExpiringUsersAsync(appSettings);
                
                log.Info($"Bulundu: {users.Count()} kullanıcı");

                if (args.Contains("--send") && users.Any())
                {
                    log.Info("E-postalar gönderiliyor...");
                    var (successCount, failCount) = await email.SendWarningsAsync(appSettings, users);
                    log.Info($"Gönderim tamamlandı: Başarılı={successCount}, Hatalı={failCount}");

                    // Yönetici maili
                    if (appSettings.System.SendAdminSummary && !string.IsNullOrWhiteSpace(appSettings.System.AdminSummaryEmail))
                    {
                        var adminSubject = $"[Özet] Şifre Dolma Uyarısı Gönderimi - {DateTime.Now:yyyy-MM-dd HH:mm}";
                        var userListHtml = GenerateUserListHtmlHeadless(users);
                        
                        var adminBody = $@"<html><body style=""font-family: Arial, sans-serif; color: #333; line-height: 1.6;"">
<h2>Şifre Dolma Uyarısı — Gönderim Özeti</h2>

<div style=""background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;"">
  <p><strong>Özet İstatistikler:</strong></p>
  <ul>
    <li><strong>Başarılı:</strong> {successCount}</li>
    <li><strong>Hatalı:</strong> {failCount}</li>
    <li><strong>Toplam Kullanıcı:</strong> {users.Count()}</li>
    <li><strong>Tarih/Saat:</strong> {DateTime.Now:G}</li>
    <li><strong>Çalışma Modu:</strong> Headless (Task Scheduler)</li>
  </ul>
</div>

<h3>Uyarı Gönderilen Kullanıcılar:</h3>
{userListHtml}

<p style=""color: #666; font-size: 12px; margin-top: 30px;"">
  <em>Bu e-posta otomatik olarak gönderilmiştir. Lütfen yanıt vermeyiniz.</em>
</p>
</body></html>";
                        var adminResult = await email.SendAsync(
                            appSettings.Smtp,
                            appSettings.System.AdminSummaryEmail,
                            adminSubject,
                            adminBody);

                        if (adminResult.Success)
                            log.Info($"Yönetici özeti gönderildi: {appSettings.System.AdminSummaryEmail}");
                        else
                            log.Error($"Yönetici özeti gönderilemedi ({appSettings.System.AdminSummaryEmail}): {adminResult.Message}");
                    }
                }
            }

            log.Info("=== Headless Mode Tamamlandı ===");
            return 0;
        }
        catch (Exception ex)
        {
            log.Error($"Headless Mode Hatası: {ex.Message}");
            return 1;
        }
    }

    private static string GenerateUserListHtmlHeadless(IEnumerable<UserModel> users)
    {
        var userList = users.ToList();
        if (!userList.Any())
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

        for (int i = 0; i < userList.Count; i++)
        {
            var user = userList[i];
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
