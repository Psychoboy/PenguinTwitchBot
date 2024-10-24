namespace DotNetTwitchBot.Repository
{
    public interface IUnitOfWork : IDisposable
    {
        IAudioCommandsRepository AudioCommands { get; }
        IDefaultCommandRepository DefaultCommands { get; }
        ISongRequestMetricsRepository SongRequestMetrics { get; }
        ISongRequestMetricsWithRankRepository SongRequestMetricsWithRank { get; }
        ISongRequestHistoryRepository SongRequestHistory { get; }
        ISongRequestHistoryWithRankRepository SongRequestHistoryWithRank { get; }
        IRaidHistoryRepository RaidHistory { get; }
        ITicketsRepository ViewerTickets { get; }
        ITicketsWithRankRepository ViewerTicketsWithRank { get; }
        IViewersRepository Viewers { get; }
        IAliasRepository Aliases { get; }
        ISettingsRepository Settings { get; }
        IGiveawayEntriesRepository GiveawayEntries { get; }
        IGiveawayWinnersRepository GiveawayWinners { get; }
        IGiveawayExclusionRepository GiveawayExclusions { get; }
        ITimerGroupsRepository TimerGroups { get; }
        IDeathCountersRepository DeathCounters { get; }
        IViewerMessageCountsRepository ViewerMessageCounts { get; }
        IViewerMessageCountsWithRankRepository ViewerMessageCountsWithRank { get; }
        IViewerPointsRepository ViewerPoints { get; }
        IViewerPointWithRanksRepository ViewerPointWithRanks { get; }
        IViewersTimeRepository ViewersTime { get; }
        IViewersTimeWithRankRepository ViewersTimeWithRank { get; }
        IViewerChatHistoriesRepository ViewerChatHistories { get; }
        ICustomCommandsRepository CustomCommands { get; }
        IKeywordsRepository Keywords { get; }
        ICountersRepository Counters { get; }
        IQuotesRepository Quotes { get; }
        IAutoShoutoutsRepository AutoShoutouts { get; }
        ITimerMessagesRepository TimerMessages { get; }
        IWordFiltersRepository WordFilters { get; }
        IKnownBotsRepository KnownBots { get; }
        IPlaylistsRepository Playlists { get; }
        ISubscriptionHistoriesRepository SubscriptionHistories { get; }
        ISongsRepository Songs { get; }
        ISongRequestViewItemsRepository SongRequestViewItems { get; }
        IExternalCommandsRepository ExternalCommands { get; }
        IBannedViewersRepository BannedViewers { get; }
        IFilteredQuotesRepository FilteredQuotes { get; }
        IRegisteredVoiceRepository RegisteredVoices { get; }
        IUserRegisteredVoicesRepository UserRegisteredVoices { get; }
        IChannelPointRedeemsRepository ChannelPointRedeems { get; }
        ITwitchEventsRepository TwitchEvents { get; }
        IDiscordTwitchScheduleMapRepository DiscordTwitchEventMap { get; }
        IIpLogRepository IpLogs { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}
