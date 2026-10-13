using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

public sealed class EmailService : IEmailService
{
    private readonly ILogService _log;

    public EmailService(ILogService log)
    {
        _log = log;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        SmtpConfigModel smtp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient();
            await ConnectAsync(client, smtp, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            _log.Info("SMTP bağlantı testi başarılı.");
            return (true, "SMTP bağlantısı başarılı.");
        }
        catch (Exception ex)
        {
            var detail = FormatException(ex);
            _log.Error($"SMTP bağlantı testi başarısız: {detail}");
            return (false, detail);
        }
    }

    public async Task<(bool Success, string Message)> SendAsync(
        SmtpConfigModel smtp,
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(smtp.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await ConnectAsync(client, smtp, cancellationToken).ConfigureAwait(false);
            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);

            _log.Info($"E-posta gönderildi: {to}");
            return (true, "Gönderildi");
        }
        catch (Exception ex)
        {
            var detail = FormatException(ex);
            _log.Error($"E-posta gönderilemedi ({to}): {detail}");
            return (false, detail);
        }
    }

    public async Task<(int SuccessCount, int FailCount)> SendWarningsAsync(
        AppSettingsModel settings,
        IEnumerable<UserModel> recipients,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var list = recipients.Where(r => r.IsSelected && !string.IsNullOrWhiteSpace(r.Email)).ToList();
        var success = 0;
        var fail = 0;

        for (var i = 0; i < list.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = list[i];
            var subject = TemplateRenderer.Render(settings.EmailTemplate.Subject, user, settings.CompanyName);
            var body = TemplateRenderer.Render(settings.EmailTemplate.HtmlBody, user, settings.CompanyName);

            progress?.Report((i + 1, list.Count, $"Gönderiliyor: {user.Email}"));
            var result = await SendAsync(settings.Smtp, user.Email, subject, body, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success) success++;
            else fail++;

            if (settings.System.SmtpDelayMilliseconds > 0 && i < list.Count - 1)
                await Task.Delay(settings.System.SmtpDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report((list.Count, list.Count, $"Tamamlandı. Başarılı: {success}, Hatalı: {fail}"));
        return (success, fail);
    }

    private static async Task ConnectAsync(SmtpClient client, SmtpConfigModel smtp, CancellationToken ct)
    {
        // Certificate revocation check'i offline olabilir (özellikle Server 2025'te)
        // Bu durumda, sertifikanın validity'sini doğrula ama revocation check'i atla
        client.ServerCertificateValidationCallback = (s, certificate, chain, sslPolicyErrors) =>
        {
            // Sertifika zinciri eksikse veya self-signed ise reddet
            if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                return true;

            // Revocation check hatasını tolere et (çoğunlukla offline revocation server)
            if ((sslPolicyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors) != 0 &&
                chain?.ChainStatus != null)
            {
                // Sadece revocation-related errorlar ise, geç
                var hasOnlyRevocationErrors = chain.ChainStatus.All(status =>
                    status.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.RevocationStatusUnknown ||
                    status.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.OfflineRevocation);

                if (hasOnlyRevocationErrors)
                    return true;
            }

            // Diğer SSL hataları reddet
            return false;
        };

        var secure = ResolveSecureSocketOptions(smtp);
        await client.ConnectAsync(smtp.Server, smtp.Port, secure, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Port-aware TLS: 465 = implicit SSL, 587 = STARTTLS (Brevo / PowerShell EnableSsl),
    /// other ports follow UseSsl or opportunistically upgrade.
    /// </summary>
    internal static SecureSocketOptions ResolveSecureSocketOptions(SmtpConfigModel smtp) =>
        smtp.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => smtp.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.StartTlsWhenAvailable
        };

    private static string FormatException(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(e.Message) &&
                (parts.Count == 0 || !string.Equals(parts[^1], e.Message, StringComparison.Ordinal)))
            {
                parts.Add(e.Message);
            }
        }

        return parts.Count == 0 ? ex.GetType().Name : string.Join(" → ", parts);
    }
}
