using System.DirectoryServices.AccountManagement;
using PasswordExpiryNotification.Models;

namespace PasswordExpiryNotification.Services;

/// <summary>
/// AD tarama iskeleti. Domain ortamında çalışır; geliştirme makinesinde hata loglanır.
/// FGPP (msDS-ResultantPSO) sonraki iterasyonda genişletilecek.
/// </summary>
public sealed class AdService : IAdService
{
    private readonly ILogService _log;

    public AdService(ILogService log)
    {
        _log = log;
    }

    public Task<IReadOnlyList<UserModel>> ScanExpiringUsersAsync(
        AppSettingsModel settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(settings, progress, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<UserModel> Scan(
        AppSettingsModel settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var selectedDays = settings.SelectedWarningDays.ToHashSet();
        if (selectedDays.Count == 0)
        {
            _log.Warning("Hiçbir uyarı günü seçili değil; tarama boş dönecek.");
            return Array.Empty<UserModel>();
        }

        var results = new List<UserModel>();
        var today = DateTime.Today;
        var maxAgeFallback = settings.AdPolicy.DefaultMaxPasswordAgeDays;

        progress?.Report("Active Directory bağlantısı kuruluyor...");
        _log.Info("AD taraması başladı.");

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var searcher = new PrincipalSearcher(new UserPrincipal(context) { Enabled = true });

            foreach (var principal in searcher.FindAll())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (principal is not UserPrincipal user)
                    continue;

                if (settings.AdPolicy.ExcludePasswordNeverExpires && user.PasswordNeverExpires)
                    continue;

                if (settings.AdPolicy.ExcludeDisabled && user.Enabled == false)
                    continue;

                var email = user.EmailAddress ?? string.Empty;
                if (settings.AdPolicy.ExcludeEmptyEmail && string.IsNullOrWhiteSpace(email))
                    continue;

                var lastSet = user.LastPasswordSet?.Date;
                if (lastSet is null)
                    continue;

                // TODO: FGPP / msDS-ResultantPSO ile kullanıcıya özel max age
                var maxAge = maxAgeFallback;
                var expiry = lastSet.Value.AddDays(maxAge);
                var daysToExpire = (expiry - today).Days;

                if (!selectedDays.Contains(daysToExpire))
                    continue;

                results.Add(new UserModel
                {
                    IsSelected = true,
                    DisplayName = user.DisplayName ?? user.SamAccountName ?? string.Empty,
                    UserName = user.SamAccountName ?? string.Empty,
                    Email = email,
                    LastPasswordSet = lastSet,
                    PasswordExpiryDate = expiry,
                    DaysToExpire = daysToExpire,
                    MatchedWarningDay = daysToExpire,
                    OrganizationalUnit = user.DistinguishedName ?? string.Empty
                });
            }

            progress?.Report($"Tarama tamamlandı: {results.Count} kullanıcı.");
            _log.Info($"AD taraması tamamlandı. Eşleşen: {results.Count} (seçili günler: {string.Join(",", selectedDays.OrderByDescending(x => x))})");
        }
        catch (Exception ex)
        {
            _log.Error($"AD taraması başarısız: {ex.Message}");
            progress?.Report($"Hata: {ex.Message}");
            throw;
        }

        return results
            .OrderBy(u => u.DaysToExpire)
            .ThenBy(u => u.DisplayName)
            .ToList();
    }
}
