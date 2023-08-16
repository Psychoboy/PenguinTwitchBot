using DotNetTwitchBot.Bot.DataAccess;

namespace DotNetTwitchBot.Bot.Repository
{
    public interface IUnitOfWork :IDisposable
    {
        IAudioCommandsRepository AudioCommands { get; }
        IDefaultCommandRepository DefaultCommands { get; }
        ISongRequestMetricsRepository SongRequestMetrics { get; }
        IRaidHistoryRepository RaidHistory { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}
