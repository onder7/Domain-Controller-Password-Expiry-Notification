using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordExpiryNotification.Services;

namespace PasswordExpiryNotification.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IAdService _adService;
    private readonly ILogService _log;

    [ObservableProperty]
    private string _customWarningDayText = string.Empty;

    public ScanViewModel(MainViewModel main, IAdService adService, ILogService log)
    {
        _main = main;
        _adService = adService;
        _log = log;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (_main.Settings.SelectedWarningDays.Count == 0)
        {
            _main.StatusText = "En az bir uyarı günü seçmelisiniz (Ayarlar).";
            _log.Warning(_main.StatusText);
            return;
        }

        try
        {
            _main.IsBusy = true;
            _main.ProgressValue = 0;
            _main.StatusText = "AD taranıyor...";
            _main.ScannedUsers.Clear();

            var progress = new Progress<string>(msg =>
            {
                _main.StatusText = msg;
                _main.ProgressValue = Math.Min(90, _main.ProgressValue + 5);
            });

            var users = await _adService.ScanExpiringUsersAsync(_main.Settings, progress).ConfigureAwait(true);

            foreach (var user in users)
                _main.ScannedUsers.Add(user);

            _main.ProgressValue = 100;
            _main.StatusText = $"Tarama bitti: {users.Count} kullanıcı (seçili günler: {string.Join(", ", _main.Settings.SelectedWarningDays)})";

            if (_main.Settings.System.OperationMode == Models.OperationMode.Automatic &&
                _main.Settings.System.AutoSendEnabled)
            {
                _main.MoveScanResultsToPreview();
                await _main.PreviewVm.ConfirmAndSendCommand.ExecuteAsync(null);
            }
            else if (users.Count > 0)
            {
                _main.MoveScanResultsToPreview();
            }
        }
        catch (Exception ex)
        {
            _main.StatusText = $"Tarama hatası: {ex.Message}";
            _log.Error(_main.StatusText);
        }
        finally
        {
            _main.IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var u in _main.ScannedUsers)
            u.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var u in _main.ScannedUsers)
            u.IsSelected = false;
    }

    [RelayCommand]
    private void SendSelectedToPreview() => _main.MoveScanResultsToPreview();

    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"PasswordExpiry_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("DisplayName,UserName,Email,LastPasswordSet,ExpiryDate,DaysToExpire,MatchedWarningDay");
            foreach (var u in _main.ScannedUsers)
            {
                sb.AppendLine(string.Join(",",
                    Csv(u.DisplayName),
                    Csv(u.UserName),
                    Csv(u.Email),
                    u.LastPasswordSet?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                    u.PasswordExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                    u.DaysToExpire,
                    u.MatchedWarningDay));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _main.StatusText = $"CSV dışa aktarıldı: {path}";
            _log.Info(_main.StatusText);
        }
        catch (Exception ex)
        {
            _log.Error($"CSV export hatası: {ex.Message}");
        }
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
