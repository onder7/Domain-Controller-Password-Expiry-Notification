using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

public interface ISettingsService
{
    AppSettingsModel Load();
    void Save(AppSettingsModel settings);
    string SettingsPath { get; }
}
