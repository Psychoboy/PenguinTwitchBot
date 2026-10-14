namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public interface IDropRegistry
    {
        void RegisterDrop(PendingDropRecord record);
        PendingDropRecord? GetPendingDrop(string dropToken);
        IReadOnlyList<PendingDropRecord> GetActiveDrops(DropGameType? gameType = null);
        bool TryConsumeDrop(string dropToken, out PendingDropRecord? record, out string? failureReason);
        void CleanupExpiredDrops();
    }
}

