using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

public interface IAdService
{
    Task<IReadOnlyList<UserModel>> ScanExpiringUsersAsync(
        AppSettingsModel settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
