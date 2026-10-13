using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogService _log;

    public SettingsService(ILogService log)
    {
        _log = log;
        SettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public string SettingsPath { get; }

    public AppSettingsModel Load()
    {
        var settings = new AppSettingsModel();
        settings.EnsureDefaultWarningDays();

        try
        {
            if (!File.Exists(SettingsPath))
            {
                _log.Warning($"Ayar dosyası bulunamadı, varsayılanlar kullanılıyor: {SettingsPath}");
                Save(settings);
                return settings;
            }

            var json = File.ReadAllText(SettingsPath);
            var dto = JsonSerializer.Deserialize<AppSettingsDto>(json, JsonOptions) ?? new AppSettingsDto();
            MapFromDto(dto, settings);
            settings.Smtp.Password = DpapiProtectionService.Unprotect(settings.Smtp.PasswordProtected);
            _log.Info("Ayarlar yüklendi.");
        }
        catch (Exception ex)
        {
            _log.Error($"Ayarlar yüklenemedi: {ex.Message}");
            settings.EnsureDefaultWarningDays();
        }

        return settings;
    }

    public void Save(AppSettingsModel settings)
    {
        try
        {
            if (!string.IsNullOrEmpty(settings.Smtp.Password))
                settings.Smtp.PasswordProtected = DpapiProtectionService.Protect(settings.Smtp.Password);

            var dto = MapToDto(settings);
            var json = JsonSerializer.Serialize(dto, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            _log.Info("Ayarlar kaydedildi.");
        }
        catch (Exception ex)
        {
            _log.Error($"Ayarlar kaydedilemedi: {ex.Message}");
            throw;
        }
    }

    private static void MapFromDto(AppSettingsDto dto, AppSettingsModel settings)
    {
        settings.CompanyName = dto.CompanyName ?? settings.CompanyName;

        if (dto.Smtp is not null)
        {
            settings.Smtp.Server = dto.Smtp.Server ?? settings.Smtp.Server;
            settings.Smtp.Port = dto.Smtp.Port;
            settings.Smtp.FromAddress = dto.Smtp.FromAddress ?? settings.Smtp.FromAddress;
            settings.Smtp.Username = dto.Smtp.Username ?? string.Empty;
            settings.Smtp.PasswordProtected = dto.Smtp.PasswordProtected ?? string.Empty;
            settings.Smtp.UseSsl = dto.Smtp.UseSsl;
        }

        if (dto.AdPolicy is not null)
        {
            settings.AdPolicy.DefaultMaxPasswordAgeDays = dto.AdPolicy.DefaultMaxPasswordAgeDays;
            settings.AdPolicy.UseFineGrainedPasswordPolicy = dto.AdPolicy.UseFineGrainedPasswordPolicy;
            settings.AdPolicy.OrganizationalUnitFilter = dto.AdPolicy.OrganizationalUnitFilter ?? string.Empty;
            settings.AdPolicy.SecurityGroupFilter = dto.AdPolicy.SecurityGroupFilter ?? string.Empty;
            settings.AdPolicy.ExcludeDisabled = dto.AdPolicy.ExcludeDisabled;
            settings.AdPolicy.ExcludePasswordNeverExpires = dto.AdPolicy.ExcludePasswordNeverExpires;
            settings.AdPolicy.ExcludeEmptyEmail = dto.AdPolicy.ExcludeEmptyEmail;
        }

        if (dto.EmailTemplate is not null)
        {
            settings.EmailTemplate.Subject = dto.EmailTemplate.Subject ?? EmailTemplateModel.DefaultSubject;
            settings.EmailTemplate.HtmlBody = dto.EmailTemplate.HtmlBody ?? EmailTemplateModel.DefaultHtmlBody;
        }

        if (dto.System is not null)
        {
            settings.System.OperationMode = Enum.TryParse<OperationMode>(dto.System.OperationMode, true, out var mode)
                ? mode
                : OperationMode.Manual;
            settings.System.AutoScanEnabled = dto.System.AutoScanEnabled;
            settings.System.AutoSendEnabled = dto.System.AutoSendEnabled;
            settings.System.ScheduledTime = dto.System.ScheduledTime ?? "08:00";
            settings.System.SendAdminSummary = dto.System.SendAdminSummary;
            settings.System.AdminSummaryEmail = dto.System.AdminSummaryEmail ?? string.Empty;
            settings.System.SmtpDelayMilliseconds = dto.System.SmtpDelayMilliseconds;
            settings.System.LogDirectory = dto.System.LogDirectory ?? "Logs";
        }

        settings.WarningDayOptions.Clear();
        var available = dto.WarningDays?.AvailableDays?.Distinct().OrderByDescending(x => x).ToList()
                        ?? new List<int> { 30, 14, 7, 3, 1 };
        var selected = new HashSet<int>(dto.WarningDays?.SelectedDays ?? new List<int> { 14, 7, 3, 1 });

        foreach (var day in available)
            settings.WarningDayOptions.Add(new WarningDayOption(day, selected.Contains(day)));

        if (settings.WarningDayOptions.Count == 0)
            settings.EnsureDefaultWarningDays();
    }

    private static AppSettingsDto MapToDto(AppSettingsModel settings) => new()
    {
        CompanyName = settings.CompanyName,
        Smtp = new SmtpDto
        {
            Server = settings.Smtp.Server,
            Port = settings.Smtp.Port,
            FromAddress = settings.Smtp.FromAddress,
            Username = settings.Smtp.Username,
            PasswordProtected = settings.Smtp.PasswordProtected,
            UseSsl = settings.Smtp.UseSsl
        },
        AdPolicy = new AdPolicyDto
        {
            DefaultMaxPasswordAgeDays = settings.AdPolicy.DefaultMaxPasswordAgeDays,
            UseFineGrainedPasswordPolicy = settings.AdPolicy.UseFineGrainedPasswordPolicy,
            OrganizationalUnitFilter = settings.AdPolicy.OrganizationalUnitFilter,
            SecurityGroupFilter = settings.AdPolicy.SecurityGroupFilter,
            ExcludeDisabled = settings.AdPolicy.ExcludeDisabled,
            ExcludePasswordNeverExpires = settings.AdPolicy.ExcludePasswordNeverExpires,
            ExcludeEmptyEmail = settings.AdPolicy.ExcludeEmptyEmail
        },
        WarningDays = new WarningDaysDto
        {
            AvailableDays = settings.WarningDayOptions.Select(d => d.Days).OrderByDescending(d => d).ToList(),
            SelectedDays = settings.SelectedWarningDays.ToList()
        },
        EmailTemplate = new EmailTemplateDto
        {
            Subject = settings.EmailTemplate.Subject,
            HtmlBody = settings.EmailTemplate.HtmlBody
        },
        System = new SystemDto
        {
            OperationMode = settings.System.OperationMode.ToString(),
            AutoScanEnabled = settings.System.AutoScanEnabled,
            AutoSendEnabled = settings.System.AutoSendEnabled,
            ScheduledTime = settings.System.ScheduledTime,
            SendAdminSummary = settings.System.SendAdminSummary,
            AdminSummaryEmail = settings.System.AdminSummaryEmail,
            SmtpDelayMilliseconds = settings.System.SmtpDelayMilliseconds,
            LogDirectory = settings.System.LogDirectory
        }
    };

    private sealed class AppSettingsDto
    {
        public string? CompanyName { get; set; }
        public SmtpDto? Smtp { get; set; }
        public AdPolicyDto? AdPolicy { get; set; }
        public WarningDaysDto? WarningDays { get; set; }
        public EmailTemplateDto? EmailTemplate { get; set; }
        public SystemDto? System { get; set; }
    }

    private sealed class SmtpDto
    {
        public string? Server { get; set; }
        public int Port { get; set; }
        public string? FromAddress { get; set; }
        public string? Username { get; set; }
        public string? PasswordProtected { get; set; }
        public bool UseSsl { get; set; }
    }

    private sealed class AdPolicyDto
    {
        public int DefaultMaxPasswordAgeDays { get; set; }
        public bool UseFineGrainedPasswordPolicy { get; set; }
        public string? OrganizationalUnitFilter { get; set; }
        public string? SecurityGroupFilter { get; set; }
        public bool ExcludeDisabled { get; set; }
        public bool ExcludePasswordNeverExpires { get; set; }
        public bool ExcludeEmptyEmail { get; set; }
    }

    private sealed class WarningDaysDto
    {
        public List<int>? AvailableDays { get; set; }
        public List<int>? SelectedDays { get; set; }
    }

    private sealed class EmailTemplateDto
    {
        public string? Subject { get; set; }
        public string? HtmlBody { get; set; }
    }

    private sealed class SystemDto
    {
        public string? OperationMode { get; set; }
        public bool AutoScanEnabled { get; set; }
        public bool AutoSendEnabled { get; set; }
        public string? ScheduledTime { get; set; }
        public bool SendAdminSummary { get; set; }
        public string? AdminSummaryEmail { get; set; }
        public int SmtpDelayMilliseconds { get; set; }
        public string? LogDirectory { get; set; }
    }
}
