using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordExpiryNotification.Models;
using PasswordExpiryNotification.Services;

namespace PasswordExpiryNotification.ViewModels;

public partial class PreviewViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IEmailService _emailService;
    private readonly ILogService _log;

    [ObservableProperty]
    private UserModel? _selectedUser;

    [ObservableProperty]
    private string _previewSubject = string.Empty;

    [ObservableProperty]
    private string _previewBody = string.Empty;

    public PreviewViewModel(MainViewModel main, IEmailService emailService, ILogService log)
    {
        _main = main;
        _emailService = emailService;
        _log = log;
    }

    public string SummaryText =>
        $"{_main.PreviewUsers.Count(u => u.IsSelected)} / {_main.PreviewUsers.Count} kullanıcıya uyarı hazır";

    partial void OnSelectedUserChanged(UserModel? value) => RefreshMailPreview();

    public void RefreshMailPreview()
    {
        if (SelectedUser is null)
        {
            PreviewSubject = string.Empty;
            PreviewBody = string.Empty;
            return;
        }

        PreviewSubject = TemplateRenderer.Render(
            _main.Settings.EmailTemplate.Subject, SelectedUser, _main.Settings.CompanyName);
        PreviewBody = TemplateRenderer.Render(
            _main.Settings.EmailTemplate.HtmlBody, SelectedUser, _main.Settings.CompanyName);
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private void CancelToScan()
    {
        _main.SelectedTabIndex = 0;
        _main.StatusText = "Önizleme iptal edildi.";
    }

    [RelayCommand]
    private async Task ConfirmAndSendAsync()
    {
        if (_main.Settings.System.OperationMode == OperationMode.Manual &&
            !_main.Settings.System.AutoSendEnabled)
        {
            // Manuel: bu ekrandan onay şart
        }

        var recipients = _main.PreviewUsers.Where(u => u.IsSelected).ToList();
        if (recipients.Count == 0)
        {
            _main.StatusText = "Gönderilecek kullanıcı seçilmedi.";
            return;
        }

        try
        {
            _main.IsBusy = true;
            _main.ProgressValue = 0;
            _main.StatusText = "Uyarılar gönderiliyor...";

            var progress = new Progress<(int Current, int Total, string Status)>(p =>
            {
                _main.ProgressValue = p.Total == 0 ? 0 : (p.Current * 100.0 / p.Total);
                _main.StatusText = $"Gönderilen: {p.Current}/{p.Total} — {p.Status}";
            });

            var result = await _emailService.SendWarningsAsync(
                _main.Settings, recipients, progress).ConfigureAwait(true);

            _main.StatusText = $"Gönderim bitti. Başarılı: {result.SuccessCount}, Hatalı: {result.FailCount}";
            _log.Info(_main.StatusText);
            OnPropertyChanged(nameof(SummaryText));
        }
        catch (Exception ex)
        {
            _main.StatusText = $"Gönderim hatası: {ex.Message}";
            _log.Error(_main.StatusText);
        }
        finally
        {
            _main.IsBusy = false;
        }
    }
}
