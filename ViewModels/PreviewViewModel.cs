using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net;
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

            if (_main.Settings.System.SendAdminSummary &&
                !string.IsNullOrWhiteSpace(_main.Settings.System.AdminSummaryEmail))
            {
                var adminSubject = $"[Özet] Şifre Dolma Uyarısı Gönderimi - {DateTime.Now:yyyy-MM-dd HH:mm}";
                var adminBody = BuildAdminSummaryHtml(recipients, result.SuccessCount, result.FailCount);
                var adminResult = await _emailService.SendAsync(
                    _main.Settings.Smtp,
                    _main.Settings.System.AdminSummaryEmail,
                    adminSubject,
                    adminBody).ConfigureAwait(true);

                if (adminResult.Success)
                    _log.Info($"Yönetici özeti gönderildi: {_main.Settings.System.AdminSummaryEmail}");
                else
                    _log.Error($"Yönetici özeti gönderilemedi ({_main.Settings.System.AdminSummaryEmail}): {adminResult.Message}");
            }

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

    private static string BuildAdminSummaryHtml(
        IReadOnlyList<UserModel> users,
        int successCount,
        int failCount)
    {
        var rows = string.Join(Environment.NewLine, users.Select(user => $"""
            <tr>
              <td style="padding:8px;border:1px solid #ddd;">{WebUtility.HtmlEncode(user.DisplayName)}</td>
              <td style="padding:8px;border:1px solid #ddd;">{WebUtility.HtmlEncode(user.Email)}</td>
              <td style="padding:8px;border:1px solid #ddd;text-align:center;">{user.DaysToExpire}</td>
            </tr>
            """));

        return $"""
            <html><body style="font-family:Arial,sans-serif;color:#333;">
            <h2>Şifre Dolma Uyarısı — Gönderim Özeti</h2>
            <ul>
              <li><strong>Başarılı:</strong> {successCount}</li>
              <li><strong>Hatalı:</strong> {failCount}</li>
              <li><strong>Toplam:</strong> {users.Count}</li>
              <li><strong>Tarih/Saat:</strong> {DateTime.Now:G}</li>
            </ul>
            <table style="width:100%;border-collapse:collapse;">
              <thead><tr style="background:#1F4E79;color:white;">
                <th style="padding:8px;text-align:left;">Kullanıcı</th>
                <th style="padding:8px;text-align:left;">E-posta</th>
                <th style="padding:8px;text-align:center;">Kalan Gün</th>
              </tr></thead>
              <tbody>{rows}</tbody>
            </table>
            </body></html>
            """;
    }
}
