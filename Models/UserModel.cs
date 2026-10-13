using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public partial class UserModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private DateTime? _lastPasswordSet;

    [ObservableProperty]
    private DateTime? _passwordExpiryDate;

    [ObservableProperty]
    private int _daysToExpire;

    [ObservableProperty]
    private int _matchedWarningDay;

    [ObservableProperty]
    private string _organizationalUnit = string.Empty;
}
