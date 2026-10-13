using System.Collections.Concurrent;
using System.IO;

namespace PasswordExpiryNotification.Services;

public sealed class LogService : ILogService
{
    private readonly ConcurrentQueue<string> _entries = new();
    private readonly object _fileLock = new();
    private string _logDirectory = "Logs";

    public event Action<string>? LogAdded;

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public void Configure(string logDirectory)
    {
        _logDirectory = string.IsNullOrWhiteSpace(logDirectory) ? "Logs" : logDirectory;
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        _entries.Enqueue(line);
        LogAdded?.Invoke(line);

        try
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"PasswordExpiry_{DateTime.Now:yyyyMMdd}.log");
            lock (_fileLock)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // File logging must not break the UI.
        }
    }
}
