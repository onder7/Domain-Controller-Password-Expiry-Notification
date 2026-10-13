namespace PasswordExpiryNotification.Services;

public interface ILogService
{
    event Action<string>? LogAdded;
    IReadOnlyList<string> Entries { get; }
    void Info(string message);
    void Warning(string message);
    void Error(string message);
}
