namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public interface IDropRegistry
    {
        void RegisterDrop(PendingDropRecord record);
        PendingDropRecord? GetPendingDrop(string dropToken);
        IReadOnlyList<PendingDropRecord> GetActiveDrops(DropGameType? gameType = null);
        bool TryBeginCompletion(string dropToken, out PendingDropRecord? record, out string? failureReason);
        void AcknowledgeCompletion(string dropToken);
        bool TryConsumeDrop(string dropToken, out PendingDropRecord? record, out string? failureReason);
        void CleanupExpiredDrops();
        void RegisterAllowedProxyUrl(string? url, TimeSpan? lifetime = null);
        bool IsProxyUrlAllowed(string url);
    }
}
