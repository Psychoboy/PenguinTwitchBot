using DotNetTwitchBot.Repository.Repositories;

namespace DotNetTwitchBot.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            AudioCommands = new AudioCommandsRepository(_context);
            DefaultCommands = new DefaultCommandRepository(_context);
            SongRequestMetrics = new SongRequestMetricsRepository(_context);
            RaidHistory = new RaidHistoryRepository(_context);
            Viewers = new ViewersRepository(_context);
            Aliases = new AliasRepository(_context);
            Settings = new SettingsRepository(_context);
            GiveawayEntries = new GiveawayEntriesRepository(_context);
            GiveawayWinners = new GiveawayWinnersRepository(_context);
            GiveawayExclusions = new GiveawayExclusionRepository(_context);
            TimerGroups = new TimerGroupsRepository(_context);
            DeathCounters = new DeathCountersRepository(_context);
            ViewerMessageCounts = new ViewerMessageCountsRepository(_context);
            ViewerMessageCountsWithRank = new ViewerMessageCountsWithRankRepository(_context);
            ViewersTime = new ViewersTimeRepository(_context);
            ViewersTimeWithRank = new ViewersTimeWithRankRepository(_context);
            CustomCommands = new CustomCommandsRepository(_context);
            Keywords = new KeywordsRepository(_context);
            Counters = new CountersRepository(_context);
            Quotes = new QuotesRepository(_context);
            AutoShoutouts = new AutoShoutoutsRepository(_context);
            TimerMessages = new TimerMessagesRepository(_context);
            WordFilters = new WordFiltersRepository(_context);
            KnownBots = new KnowBotsRepository(_context);
            Playlists = new PlaylistsRepository(_context);
            SubscriptionHistories = new SubscriptionHistoriesRepository(_context);
            Songs = new SongsRepository(_context);
            SongRequestViewItems = new SongRequestViewItemsRepository(_context);
            SongRequestMetricsWithRank = new SongRequestMetricsWithRankRepository(_context);
            ExternalCommands = new ExternalCommandsRepository(_context);
            BannedViewers = new BannedViewersRepository(_context);
            FilteredQuotes = new FilteredQuotesRepository(_context);
            ViewerChatHistories = new ViewerChatHistoriesRepository(_context);
            RegisteredVoices = new RegisteredVoiceRepository(_context);
            UserRegisteredVoices = new UserRegisteredVoicesRepository(_context);
            ChannelPointRedeems = new ChannelPointRedeemsRepository(_context);
            TwitchEvents = new TwitchEventsRepository(_context);
            DiscordTwitchEventMap = new DiscordTwitchScheduleMapRepository(_context);
            SongRequestHistory = new SongRequestHistoryRepository(_context);
            SongRequestHistoryWithRank = new SongRequestHistoryWithRankRepository(_context);
            IpLogs = new IpLogRepository(_context);
            Wheels = new WheelRepository(_context);
            WheelProperties = new WheelPropertiesRepository(_context);
            Cooldowns = new CooldownsRepository(_context);
            GameSettings = new GameSettingsRepository(_context);
            PointTypes = new PointTypesRepository(_context);
            UserPoints = new UserPointsRepository(_context);
            PointCommands = new PointCommandsRepository(_context);
            ScAiResponses = new ScAiReponsesRepository(_context);
        }

        public IAudioCommandsRepository AudioCommands { get; private set; }
        public IDefaultCommandRepository DefaultCommands { get; private set; }
        public ISongRequestMetricsRepository SongRequestMetrics { get; private set; }
        public ISongRequestHistoryRepository SongRequestHistory { get; private set; }
        public ISongRequestHistoryWithRankRepository SongRequestHistoryWithRank { get; private set; }
        public ISongRequestMetricsWithRankRepository SongRequestMetricsWithRank { get; private set; }
        public IRaidHistoryRepository RaidHistory { get; private set; }
        public IViewersRepository Viewers { get; private set; }
        public IAliasRepository Aliases { get; private set; }
        public ISettingsRepository Settings { get; private set; }
        public IGiveawayEntriesRepository GiveawayEntries { get; private set; }
        public IGiveawayWinnersRepository GiveawayWinners { get; private set; }
        public IGiveawayExclusionRepository GiveawayExclusions { get; private set; }
        public ITimerGroupsRepository TimerGroups { get; private set; }
        public IDeathCountersRepository DeathCounters { get; private set; }
        public IViewerMessageCountsRepository ViewerMessageCounts { get; private set; }
        public IViewerMessageCountsWithRankRepository ViewerMessageCountsWithRank { get; private set; }
        public IViewersTimeRepository ViewersTime { get; private set; }
        public IViewersTimeWithRankRepository ViewersTimeWithRank { get; private set; }
        public IViewerChatHistoriesRepository ViewerChatHistories { get; private set; }
        public ICustomCommandsRepository CustomCommands { get; private set; }
        public IKeywordsRepository Keywords { get; private set; }
        public ICountersRepository Counters { get; private set; }
        public IQuotesRepository Quotes { get; private set; }
        public IAutoShoutoutsRepository AutoShoutouts { get; private set; }
        public ITimerMessagesRepository TimerMessages { get; private set; }
        public IWordFiltersRepository WordFilters { get; private set; }
        public IKnownBotsRepository KnownBots { get; private set; }
        public IPlaylistsRepository Playlists { get; private set; }
        public ISubscriptionHistoriesRepository SubscriptionHistories { get; private set; }
        public ISongsRepository Songs { get; private set; }
        public ISongRequestViewItemsRepository SongRequestViewItems { get; private set; }
        public IExternalCommandsRepository ExternalCommands { get; private set; }
        public IBannedViewersRepository BannedViewers { get; private set; }
        public IFilteredQuotesRepository FilteredQuotes { get; private set; }
        public IRegisteredVoiceRepository RegisteredVoices { get; private set; }
        public IUserRegisteredVoicesRepository UserRegisteredVoices { get; private set; }
        public IChannelPointRedeemsRepository ChannelPointRedeems { get; private set; }
        public ITwitchEventsRepository TwitchEvents { get; private set; }
        public IDiscordTwitchScheduleMapRepository DiscordTwitchEventMap { get; private set; }
        public IIpLogRepository IpLogs { get; private set; }
        public IWheelRepository Wheels { get; private set; }
        public IWheelPropertiesRepository WheelProperties { get; private set; }
        public ICooldownsRepository Cooldowns { get; private set; }
        public IGameSettingsRepository GameSettings { get; private set; }
        public IPointTypesRepository PointTypes { get; private set; }
        public IUserPointsRepository UserPoints { get; private set; }
        public IPointCommandsRepository PointCommands { get; private set; }
        public IScAiReponsesRepository ScAiResponses { get; private set; }

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public Task<int> SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }
    }
}
