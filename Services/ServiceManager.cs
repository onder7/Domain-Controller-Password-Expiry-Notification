using Topshelf;
using PasswordExpiryNotification.Services;

namespace PasswordExpiryNotification.Services;

/// <summary>
/// Windows Service implementation for PasswordExpiryNotification.
/// Runs as a service when started via install/start/stop commands.
/// </summary>
public class ServiceManager
{
    private ISchedulerService? _scheduler;
    private ILogService? _log;

    public bool Start(HostControl hostControl)
    {
        try
        {
            _log = new LogService();
            var settings = new SettingsService(_log);
            var ad = new AdService(_log);
            var email = new EmailService(_log);
            
            _scheduler = new SchedulerService(ad, email, _log);
            
            // Load settings and start the scheduler
            var appSettings = settings.Load();
            _scheduler.Start(appSettings);
            
            _log.Info("=== Windows Service başladı ===");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"Windows Service başlatılamadı: {ex.Message}");
            return false;
        }
    }

    public bool Stop(HostControl hostControl)
    {
        try
        {
            _scheduler?.Stop();
            _log?.Info("=== Windows Service durduruldu ===");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"Windows Service durdurulurken hata: {ex.Message}");
            return false;
        }
    }
}
