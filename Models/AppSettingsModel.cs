using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public partial class AppSettingsModel : ObservableObject
{
    [ObservableProperty]
    private string _companyName = "Şirketiniz A.Ş.";

    [ObservableProperty]
    private SmtpConfigModel _smtp = new();

    [ObservableProperty]
    private AdPolicyModel _adPolicy = new();

    [ObservableProperty]
    private EmailTemplateModel _emailTemplate = new();

    [ObservableProperty]
    private SystemOptionsModel _system = new();

    public ObservableCollection<WarningDayOption> WarningDayOptions { get; } = new();

    public IReadOnlyList<int> SelectedWarningDays =>
        WarningDayOptions.Where(d => d.IsSelected).Select(d => d.Days).OrderByDescending(d => d).ToList();

    public void EnsureDefaultWarningDays()
    {
        if (WarningDayOptions.Count > 0)
            return;

        foreach (var day in new[] { 30, 14, 7, 3, 1 })
        {
            var selected = day is 14 or 7 or 3 or 1;
            WarningDayOptions.Add(new WarningDayOption(day, selected));
        }
    }

    public void AddCustomWarningDay(int days)
    {
        if (days <= 0)
            return;

        if (WarningDayOptions.Any(d => d.Days == days))
            return;

        WarningDayOptions.Add(new WarningDayOption(days, true));
    }
}
