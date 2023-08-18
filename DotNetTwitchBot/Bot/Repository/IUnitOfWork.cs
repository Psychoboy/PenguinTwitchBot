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
        ISettingsRepository Settings { get; }
        IGiveawayEntriesRepository GiveawayEntries { get; }
        IGiveawayWinnersRepository GiveawayWinners { get; }
        ITimerGroupsRepository TimerGroups { get; }
        IDeathCountersRepository DeathCounters { get; }
        IViewerMessageCountsRepository ViewerMessageCounts { get; }
        IViewerMessageCountsWithRankRepository ViewerMessageCountsWithRank { get; }
        IViewerPointsRepository ViewerPoints { get; }
        IViewerPointWithRanksRepository ViewerPointWithRanks { get; }
        IViewersTimeRepository ViewersTime { get; }
        IViewersTimeWithRankRepository ViewersTimeWithRank { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}
