using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordExpiryNotification.Models;

public enum OperationMode
{
    Manual,
    Automatic
}

public partial class SystemOptionsModel : ObservableObject
{
    [ObservableProperty]
    private OperationMode _operationMode = OperationMode.Manual;

    [ObservableProperty]
    private bool _autoScanEnabled;

    [ObservableProperty]
    private bool _autoSendEnabled;

    [ObservableProperty]
    private string _scheduledTime = "08:00";

    [ObservableProperty]
    private bool _sendAdminSummary = true;

    [ObservableProperty]
    private string _adminSummaryEmail = "it@company.com";

    [ObservableProperty]
    private int _smtpDelayMilliseconds = 200;

    [ObservableProperty]
    private string _logDirectory = "Logs";
}
