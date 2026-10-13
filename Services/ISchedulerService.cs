using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

/// <summary>
/// Otomatik AD taraması ve e-posta gönderimi için zamanlama servisi.
/// GUI açık kalırken Automatic modda belirtilen saatte çalışır.
/// </summary>
public interface ISchedulerService
{
    bool IsRunning { get; }

    /// <summary>
    /// Timer'ı başlat ve ayarlara göre zamanlama kur.
    /// </summary>
    void Start(AppSettingsModel settings);

    /// <summary>
    /// Timer'ı durdur.
    /// </summary>
    void Stop();

    /// <summary>
    /// Tarama tamamlandığında tetiklenir.
    /// </summary>
    event EventHandler<SchedulerScanCompletedEventArgs>? OnScanCompleted;

    /// <summary>
    /// Gönderim tamamlandığında tetiklenir.
    /// </summary>
    event EventHandler<SchedulerSendCompletedEventArgs>? OnSendCompleted;

    /// <summary>
    /// Hata durumunda tetiklenir.
    /// </summary>
    event EventHandler<SchedulerErrorEventArgs>? OnError;
}

public class SchedulerScanCompletedEventArgs : EventArgs
{
    public IReadOnlyList<UserModel> Users { get; set; } = Array.Empty<UserModel>();
    public int Count => Users.Count;
    public DateTime CompletedAt { get; set; } = DateTime.Now;
}

public class SchedulerSendCompletedEventArgs : EventArgs
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.Now;
}

public class SchedulerErrorEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.Now;
}
