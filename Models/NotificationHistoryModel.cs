namespace PasswordExpiryNotification.Models;

public class NotificationHistoryEntry
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int WarningDay { get; set; }
    public DateTime SentAtUtc { get; set; }
    public bool Success { get; set; }
}

public class NotificationHistoryModel
{
    public List<NotificationHistoryEntry> Entries { get; set; } = new();
}
