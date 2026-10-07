namespace PenguinTwitchBot.Circuit;

public interface IIpLog
{
    Task AddLogEntry(string username, string userId, string ipAddress);
    Task LogInteractionAsync(string username, string userId, string ipAddress, TimeSpan? cacheDuration = null);
    bool IsInteractionCached(string userId, string ipAddress);
    Task CleanupOldIpLogs();
}

