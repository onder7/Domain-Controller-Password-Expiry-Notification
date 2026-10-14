using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordExpiryNotification.Models;
using PasswordExpiryNotification.Services;

namespace PasswordExpiryNotification.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IEmailService _emailService;
    private readonly ILogService _log;

    [ObservableProperty]
    private AppSettingsModel _settings;

    [ObservableProperty]
    private string _customWarningDayText = string.Empty;

    [ObservableProperty]
    private string _templatePreviewSubject = string.Empty;

    [ObservableProperty]
    private string _templatePreviewBody = string.Empty;

    [ObservableProperty]
    private string _testEmailAddress = string.Empty;

    public SettingsViewModel(
        AppSettingsModel settings,
        ISettingsService settingsService,
        IEmailService emailService,
        ILogService log)
    {
        Settings = settings;
        _settingsService = settingsService;
        _emailService = emailService;
        _log = log;
        TestEmailAddress = settings.Smtp.FromAddress;
    }

    public Array OperationModes => Enum.GetValues(typeof(OperationMode));

    [RelayCommand]
    private void Save()
    {
        if (Settings.SelectedWarningDays.Count == 0)
        {
            _log.Warning("En az bir uyarı günü seçilmelidir.");
            return;
        }

        if (Settings.System.AutoSendEnabled && Settings.System.OperationMode == OperationMode.Automatic)
            _log.Warning("Otomatik gönderim açık: mailler onay beklemeden gönderilecek.");

        _settingsService.Save(Settings);
        _log.Info("Ayarlar kaydedildi.");
    }

    [RelayCommand]
    private void ResetTemplate()
    {
        Settings.EmailTemplate.ResetToDefaults();
        _log.Info("E-posta şablonu varsayılana döndürüldü.");
    }

    [RelayCommand]
    private void AddWarningDay()
    {
        if (!int.TryParse(CustomWarningDayText, out var days) || days <= 0)
        {
            _log.Warning("Geçerli bir gün sayısı girin.");
            return;
        }

        Settings.AddCustomWarningDay(days);
        CustomWarningDayText = string.Empty;
        _log.Info($"Uyarı günü eklendi: {days}");
    }

    [RelayCommand]
    private void InsertPlaceholder(string? placeholder)
    {
        if (string.IsNullOrWhiteSpace(placeholder))
            return;

        Settings.EmailTemplate.HtmlBody += placeholder;
    }

    [RelayCommand]
    private void PreviewTemplate()
    {
        var sample = new UserModel
        {
            DisplayName = "John Smith",
            UserName = "john.smith",
            Email = "john.smith@company.com",
            DaysToExpire = 7,
            MatchedWarningDay = 7,
            LastPasswordSet = DateTime.Today.AddDays(-83),
            PasswordExpiryDate = DateTime.Today.AddDays(7)
        };

        TemplatePreviewSubject = TemplateRenderer.Render(Settings.EmailTemplate.Subject, sample, Settings.CompanyName);
        TemplatePreviewBody = TemplateRenderer.Render(Settings.EmailTemplate.HtmlBody, sample, Settings.CompanyName);
    }

    [RelayCommand]
    private async Task TestSmtpAsync()
    {
        // PasswordBox syncs via MainWindow.SmtpPasswordBox_OnPasswordChanged into Settings.Smtp.Password.
        if (string.IsNullOrEmpty(Settings.Smtp.Password) && !string.IsNullOrEmpty(Settings.Smtp.PasswordProtected))
            Settings.Smtp.Password = DpapiProtectionService.Unprotect(Settings.Smtp.PasswordProtected);

        if (string.IsNullOrWhiteSpace(Settings.Smtp.Username))
            _log.Warning("SMTP kullanıcı adı boş; kimlik doğrulama atlanacak.");
        else if (string.IsNullOrEmpty(Settings.Smtp.Password))
            _log.Warning("SMTP şifresi boş; bağlantı testi kimlik doğrulamada başarısız olabilir.");

        var result = await _emailService.TestConnectionAsync(Settings.Smtp).ConfigureAwait(true);
        _log.Info(result.Success ? "SMTP test OK" : $"SMTP test başarısız: {result.Message}");
    }

    [RelayCommand]
    private async Task SendTestMailAsync()
    {
        if (string.IsNullOrEmpty(Settings.Smtp.Password) && !string.IsNullOrEmpty(Settings.Smtp.PasswordProtected))
            Settings.Smtp.Password = DpapiProtectionService.Unprotect(Settings.Smtp.PasswordProtected);

        var to = string.IsNullOrWhiteSpace(TestEmailAddress) ? Settings.Smtp.FromAddress : TestEmailAddress;
        var sample = new UserModel
        {
            DisplayName = "Test Kullanıcı",
            UserName = "test.user",
            Email = to,
            DaysToExpire = 7,
            LastPasswordSet = DateTime.Today.AddDays(-83),
            PasswordExpiryDate = DateTime.Today.AddDays(7)
        };

        var subject = TemplateRenderer.Render(Settings.EmailTemplate.Subject, sample, Settings.CompanyName);
        var body = TemplateRenderer.Render(Settings.EmailTemplate.HtmlBody, sample, Settings.CompanyName);
        var result = await _emailService.SendAsync(Settings.Smtp, to, subject, body).ConfigureAwait(true);
        _log.Info(result.Success ? $"Test maili gönderildi: {to}" : $"Test maili başarısız: {result.Message}");
    }
}
