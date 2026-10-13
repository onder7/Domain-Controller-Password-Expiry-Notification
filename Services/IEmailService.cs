using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

public interface IEmailService
{
    Task<(bool Success, string Message)> TestConnectionAsync(SmtpConfigModel smtp, CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> SendAsync(
        SmtpConfigModel smtp,
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    Task<(int SuccessCount, int FailCount)> SendWarningsAsync(
        AppSettingsModel settings,
        IEnumerable<UserModel> recipients,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        CancellationToken cancellationToken = default);
}
