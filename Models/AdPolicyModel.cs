using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public partial class AdPolicyModel : ObservableObject
{
    [ObservableProperty]
    private int _defaultMaxPasswordAgeDays = 90;

    [ObservableProperty]
    private bool _useFineGrainedPasswordPolicy = true;

    [ObservableProperty]
    private string _organizationalUnitFilter = string.Empty;

    [ObservableProperty]
    private string _securityGroupFilter = string.Empty;

    [ObservableProperty]
    private bool _excludeDisabled = true;

    [ObservableProperty]
    private bool _excludePasswordNeverExpires = true;

    [ObservableProperty]
    private bool _excludeEmptyEmail = true;
}
