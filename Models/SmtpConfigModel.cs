using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public partial class SmtpConfigModel : ObservableObject
{
    [ObservableProperty]
    private string _server = "smtp-relay.sendinblue.com";

    [ObservableProperty]
    private int _port = 587;

    [ObservableProperty]
    private string _fromAddress = "noreply@company.com";

    [ObservableProperty]
    private string _username = "smtp-username";

    /// <summary>Plain password in memory only; persisted as PasswordProtected (DPAPI).</summary>
    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _passwordProtected = string.Empty;

    [ObservableProperty]
    private bool _useSsl = true;
}
