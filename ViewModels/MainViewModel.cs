using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordExpiryNotification.Models;
using PasswordExpiryNotification.Services;

namespace PasswordExpiryNotification.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAdService _adService;
    private readonly IEmailService _emailService;
    private readonly LogService _logService;
    private readonly ISchedulerService _schedulerService;

    [ObservableProperty]
    private AppSettingsModel _settings = new();

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusText = "Hazır";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _schedulerStatus = string.Empty;

    [ObservableProperty]
    private bool _isSchedulerRunning;

    public ObservableCollection<UserModel> ScannedUsers { get; } = new();
    public ObservableCollection<UserModel> PreviewUsers { get; } = new();
    public ObservableCollection<string> LogEntries { get; } = new();

    public SettingsViewModel SettingsVm { get; }
    public ScanViewModel ScanVm { get; }
    public PreviewViewModel PreviewVm { get; }

    public MainViewModel(
        ISettingsService settingsService,
        IAdService adService,
        IEmailService emailService,
        LogService logService,
        ISchedulerService schedulerService)
    {
        _settingsService = settingsService;
        _adService = adService;
        _emailService = emailService;
        _logService = logService;
        _schedulerService = schedulerService;

        Settings = _settingsService.Load();
        _logService.Configure(Settings.System.LogDirectory);
        _logService.LogAdded += OnLogAdded;
        foreach (var entry in _logService.Entries)
            LogEntries.Add(entry);

        SettingsVm = new SettingsViewModel(Settings, _settingsService, _emailService, _logService);
        ScanVm = new ScanViewModel(this, _adService, _logService);
        PreviewVm = new PreviewViewModel(this, _emailService, _logService);

        // Scheduler event'lerini dinle
        _schedulerService.OnScanCompleted += OnSchedulerScanCompleted;
        _schedulerService.OnSendCompleted += OnSchedulerSendCompleted;
        _schedulerService.OnError += OnSchedulerError;

        // Otomatik zamanlama başlat
        StartScheduler();

        _logService.Info("Uygulama başlatıldı.");
    }

    private void OnLogAdded(string line)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Insert(0, line);
            while (LogEntries.Count > 500)
                LogEntries.RemoveAt(LogEntries.Count - 1);
        });
    }

    private void OnSchedulerScanCompleted(object? sender, SchedulerScanCompletedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ScannedUsers.Clear();
            foreach (var user in e.Users)
                ScannedUsers.Add(user);

            StatusText = $"Otomatik tarama tamamlandı: {e.Count} kullanıcı";
            _logService.Info(StatusText);

            // Uyarı Önizleme'ye aktar
            MoveScanResultsToPreview();
        });
    }

    private void OnSchedulerSendCompleted(object? sender, SchedulerSendCompletedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = $"Otomatik gönderim tamamlandı: {e.SuccessCount} başarılı, {e.FailCount} hatalı";
            _logService.Info(StatusText);
        });
    }

    private void OnSchedulerError(object? sender, SchedulerErrorEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = $"Otomatik zamanlama hatası: {e.Message}";
            _logService.Error(StatusText);
        });
    }

    private void StartScheduler()
    {
        try
        {
            _schedulerService.Start(Settings);
            IsSchedulerRunning = _schedulerService.IsRunning;

            if (IsSchedulerRunning)
            {
                SchedulerStatus = $"Otomatik tarama çalışıyor ({Settings.System.ScheduledTime})";
                _logService.Info(SchedulerStatus);
            }
            else
            {
                SchedulerStatus = "Otomatik tarama kapalı (Manuel mod)";
            }
        }
        catch (Exception ex)
        {
            SchedulerStatus = $"Zamanlama hatası: {ex.Message}";
            _logService.Error(SchedulerStatus);
        }
    }

    public void StopScheduler()
    {
        _schedulerService.Stop();
        IsSchedulerRunning = false;
        SchedulerStatus = "Otomatik tarama durduruldu";
        _logService.Info(SchedulerStatus);
    }

    public void MoveScanResultsToPreview()
    {
        PreviewUsers.Clear();
        foreach (var user in ScannedUsers.Where(u => u.IsSelected))
        {
            PreviewUsers.Add(user);
        }

        SelectedTabIndex = 1; // Uyarı Önizleme
        StatusText = $"{PreviewUsers.Count} kullanıcı önizlemeye alındı.";
        _logService.Info(StatusText);
    }

    [RelayCommand]
    private void GoToScan() => SelectedTabIndex = 0;

    [RelayCommand]
    private void GoToPreview() => SelectedTabIndex = 1;

    [RelayCommand]
    private void GoToSettings() => SelectedTabIndex = 2;
}
