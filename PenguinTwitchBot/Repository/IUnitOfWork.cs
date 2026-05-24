using Microsoft.EntityFrameworkCore.Storage;

namespace PenguinTwitchBot.Repository
{
    public interface IUnitOfWork : IDisposable
    {
        IAudioCommandsRepository AudioCommands { get; }
        IDefaultCommandRepository DefaultCommands { get; }
        ISongRequestMetricsRepository SongRequestMetrics { get; }
        ISongRequestHistoryRepository SongRequestHistory { get; }
        IRaidHistoryRepository RaidHistory { get; }
        IViewersRepository Viewers { get; }
        IAliasRepository Aliases { get; }
        ISettingsRepository Settings { get; }
        IGiveawayEntriesRepository GiveawayEntries { get; }
        IGiveawayWinnersRepository GiveawayWinners { get; }
        IGiveawayExclusionRepository GiveawayExclusions { get; }
        ITimerGroupsRepository TimerGroups { get; }
        IDeathCountersRepository DeathCounters { get; }
        IViewerMessageCountsRepository ViewerMessageCounts { get; }
        IViewersTimeRepository ViewersTime { get; }
        IViewerChatHistoriesRepository ViewerChatHistories { get; }
        IActionCommandsRepository ActionCommands { get; }
        IActionKeywordsRepository ActionKeywords { get; }
        IKeywordsRepository Keywords { get; }
        ICountersRepository Counters { get; }
        IQuotesRepository Quotes { get; }
        IAutoShoutoutsRepository AutoShoutouts { get; }
        IWordFiltersRepository WordFilters { get; }
        IKnownBotsRepository KnownBots { get; }
        IPlaylistsRepository Playlists { get; }
        ISubscriptionHistoriesRepository SubscriptionHistories { get; }
        ISongsRepository Songs { get; }
        ISongRequestViewItemsRepository SongRequestViewItems { get; }
        IExternalCommandsRepository ExternalCommands { get; }
        IBannedViewersRepository BannedViewers { get; }
        IRegisteredVoiceRepository RegisteredVoices { get; }
        IUserRegisteredVoicesRepository UserRegisteredVoices { get; }
        IDiscordTwitchScheduleMapRepository DiscordTwitchEventMap { get; }
        IIpLogRepository IpLogs { get; }
        IWheelRepository Wheels { get; }
        IWheelPropertiesRepository WheelProperties { get; }
        ICooldownsRepository Cooldowns { get; }
        IGameSettingsRepository GameSettings { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        IPointTypesRepository PointTypes { get; }
        IUserPointsRepository UserPoints { get; }
        IPointCommandsRepository PointCommands { get; }
        IScAiResponsesRepository ScAiResponses { get; }

        IActionsRepository Actions { get; }
        ISubActionsRepository SubActions { get; }
        ITriggersRepository Triggers { get; }
        IQueueConfigurationsRepository QueueConfigurations { get; }

        // Fishing repositories
        IFishingRepository FishTypes { get; }
        IFishCatchRepository FishCatches { get; }
        IFishingGoldRepository FishingGolds { get; }
        IFishingShopItemRepository FishingShopItems { get; }
        IUserFishingBoostRepository UserFishingBoosts { get; }
        IFishingSettingsRepository FishingSettings { get; }
        IFishingSnapEventRepository FishingSnapEvents { get; }
    }
}
