namespace DotNetTwitchBot.Bot.Repository
{
    public interface IUnitOfWork : IDisposable
    {
        IAudioCommandsRepository AudioCommands { get; }
        IDefaultCommandRepository DefaultCommands { get; }
        ISongRequestMetricsRepository SongRequestMetrics { get; }
        IRaidHistoryRepository RaidHistory { get; }
        ITicketsRepository ViewerTickets { get; }
        ITicketsWithRankRepository ViewerTicketsWithRank { get; }
        IViewersRepository Viewers { get; }
        IFollowerRepository Followers { get; }
        IAliasRepository Aliases { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}
