using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public partial class WarningDayOption : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private int _days;

    [ObservableProperty]
    private bool _isSelected;

    public WarningDayOption()
    {
    }

    public WarningDayOption(int days, bool isSelected)
    {
        Days = days;
        IsSelected = isSelected;
    }

    public string DisplayText => $"{Days} gün";
}
