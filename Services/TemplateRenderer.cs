using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

public static class TemplateRenderer
{
    public static string Render(string template, UserModel user, string companyName)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        return template
            .Replace("{Name}", user.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{UserName}", user.UserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{DaysToExpire}", user.DaysToExpire.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{ExpiryDate}", FormatDate(user.PasswordExpiryDate), StringComparison.OrdinalIgnoreCase)
            .Replace("{LastPasswordSet}", FormatDate(user.LastPasswordSet), StringComparison.OrdinalIgnoreCase)
            .Replace("{CompanyName}", companyName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("dd.MM.yyyy") ?? "-";
}
