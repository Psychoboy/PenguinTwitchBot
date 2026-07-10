using PenguinTwitchBot.Bot;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Admin;
using PenguinTwitchBot.Bot.Commands.Ai;
using PenguinTwitchBot.Bot.Commands.Alias;
using PenguinTwitchBot.Bot.Commands.AudioCommand;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Commands.Shoutout;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Commands.TTS;
using PenguinTwitchBot.Bot.Commands.WheelSpin;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Diagnostics;
using PenguinTwitchBot.Bot.ServiceTools;
using PenguinTwitchBot.Bot.StreamSchedule;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.CustomMiddleware
{
    public static class BotCommandsRegistry
    {
        public static IServiceCollection AddBotCommands(this IServiceCollection services)
        {
            const string helixHttpClientName = "TwitchHelix";
            const string twitchIdHttpClientName = "TwitchId";

            services.AddHttpClient(helixHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://api.twitch.tv/helix/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 100,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            });

            services.AddHttpClient(twitchIdHttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://id.twitch.tv/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 100,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            });

            services.AddTransient<IWebsocketClient, WebsocketClient>();
            services.AddSingleton<TwitchApi.EventSub.Websockets.IEventSubWebsocketClient>(x => new TwitchApi.EventSub.Websockets.EventSubWebsocketClient(x.GetRequiredService<ILogger<TwitchApi.EventSub.Websockets.EventSubWebsocketClient>>(), x.GetRequiredService<IServiceProvider>(), x.GetRequiredService<IWebsocketClient>()));

            
            services.AddSingleton<IServiceBackbone, ServiceBackbone>();
            services.AddSingleton<ITwitchService, TwitchService>();
            services.AddSingleton<IAuthTransport, AuthTransport>();
            services.AddSingleton<IAuthClient, AuthClient>();
            services.AddSingleton<IChatTransport, ChatTransport>();
            services.AddSingleton<IChatClient, ChatClient>();
            services.AddSingleton<IChannelPointsTransport, ChannelPointsTransport>();
            services.AddSingleton<IChannelPointsClient, ChannelPointsClient>();
            services.AddSingleton<IModerationTransport, ModerationTransport>();
            services.AddSingleton<IModerationClient, ModerationClient>();
            services.AddSingleton<IChannelsTransport, ChannelsTransport>();
            services.AddSingleton<IChannelsClient, ChannelsClient>();
            services.AddSingleton<IStreamsTransport, StreamsTransport>();
            services.AddSingleton<IStreamsClient, StreamsClient>();
            services.AddSingleton<IClipsTransport, ClipsTransport>();
            services.AddSingleton<IClipsClient, ClipsClient>();
            services.AddSingleton<IGamesTransport, GamesTransport>();
            services.AddSingleton<IGamesClient, GamesClient>();
            services.AddSingleton<ISubscriptionsTransport, SubscriptionsTransport>();
            services.AddSingleton<ISubscriptionsClient, SubscriptionsClient>();
            services.AddSingleton<IRaidsTransport, RaidsTransport>();
            services.AddSingleton<IRaidsClient, RaidsClient>();
            services.AddSingleton<IUsersTransport, UsersTransport>();
            services.AddSingleton<IUsersClient, UsersClient>();
            services.AddSingleton<IScheduleTransport, ScheduleTransport>();
            services.AddSingleton<IScheduleClient, ScheduleClient>();
            services.AddSingleton<ITwitchEventActionHandler, TwitchEventActionHandler>();
            services.AddTransient<ISchedule, Schedule>();
            services.AddSingleton<PenguinTwitchBot.Bot.Commands.ICommandHandler, PenguinTwitchBot.Bot.Commands.CommandHandler>();
            services.AddSingleton<PenguinTwitchBot.Bot.Commands.IDefaultCommandTriggerService, PenguinTwitchBot.Bot.Commands.DefaultCommandTriggerService>();
            services.AddSingleton<IFeatureStateStore, FeatureStateStore>();
            services.AddRuntimeFeatureService<ITwitchChatBot, TwitchChatBot>(
                FeatureKeys.TwitchChatBot,
                "Twitch Chat Bot",
                isCore: true,
                description: "The core Twitch chat bot service that sends chat messages."
            );
            services.AddRuntimeFeatureService<ITwitchWebsocketHostedService, TwitchWebsocketHostedService>(
                FeatureKeys.TwitchWebsocket,
                "Twitch Websocket",
                isCore: true,
                description: "The core Twitch websocket service that handles all events from twitch including chat messages, subscriptions, raids, and more."
            );

            services.AddRuntimeFeatureService<Bot.Commands.Features.IViewerFeature, Bot.Commands.Features.ViewerFeature>(
                FeatureKeys.ViewerFeature,
                "Viewer Feature",
                isCore: true,
                description: "Viewer feature for tracking viewer data and interactions."
            );

            // OBS WebSocket Services
            services.AddSingleton<IOBSConnectionManager, OBSConnectionManager>();
            services.AddHostedService<OBSConnectionHostedService>();

            services.AddSingleton<Bot.Notifications.IWebSocketMessenger, Bot.Notifications.WebSocketMessenger>();
            services.AddSingleton<Bot.WebSocketEvents.IWsEventHandler, Bot.WebSocketEvents.WsEventHandler>();

            services.AddRuntimeFeatureService<Bot.Commands.Moderation.IKnownBots, Bot.Commands.Moderation.KnownBots>(
                FeatureKeys.KnownBots,
                "Known Bots",
                isCore: true,
                description: "Manage known bots in the chat. Also used to identify the bot itself and the streamer as known bots."
            );

            services.AddRuntimeFeatureService<ILoyaltyFeature, LoyaltyFeature>(
                FeatureKeys.LoyaltyFeature,
                "Loyalty Feature",
                isCore: false,
                description: "Loyalty feature for tracking viewer watch time and message counts."
            );

            services.AddSingleton<ISubscriptionTracker, SubscriptionTracker>();
            // IpLog is registered in Program.cs (always) so it's available even in setup mode.

            services.AddScoped(typeof(PenguinTwitchBot.Database.Repository.IGenericRepository<>), typeof(PenguinTwitchBot.Database.Repository.Repositories.GenericRepository<>));
            services.AddScoped<PenguinTwitchBot.Database.Repository.IUnitOfWork, PenguinTwitchBot.Database.Repository.UnitOfWork>();
            services.AddScoped<Bot.Actions.IActionManagementService, Bot.Actions.ActionManagementService>();
            services.AddScoped<Bot.Actions.IRaffleSetupService, Bot.Actions.RaffleSetupService>();
            services.AddScoped<Bot.Commands.IActionCommandService, Bot.Commands.ActionCommandService>();
            services.AddScoped<Bot.Commands.IActionKeywordService, Bot.Commands.ActionKeywordService>();
            services.AddSingleton<Bot.Commands.Actions.IActionKeywordCache, Bot.Commands.Actions.ActionKeywordCache>();
            services.AddScoped<IIpLogFeature, IpLogFeature>();
            //Add Features Here:

            services.AddSingleton<Bot.Commands.PastyGames.MaxBetCalculator>();
            services.AddRuntimeFeatureService<IAlias, Alias>(
                FeatureKeys.Alias,
                "Alias",
                isCore: false,
                description: "Alias commands and viewer alias page."
            );
            //Add Alerts
            services.AddSingleton<Bot.Alerts.AlertImage>();

            services.AddRuntimeFeatureService<IBonusTickets, BonusTickets>(
                FeatureKeys.BonusTickets,
                "Bonus Points",
                isCore: false,
                description: "Bonus points claim widget and per-stream reset behavior.");
            services.AddSingleton<IRaffleRuntimeService, RaffleRuntimeService>();


            services.AddRuntimeFeatureService<IGiveawayFeature, GiveawayFeature>(
                FeatureKeys.GiveawayFeature,
                "Giveaway",
                isCore: false,
                description: "Giveaway feature."
            );
            services.AddRuntimeFeatureService<Roulette>(
                FeatureKeys.Roulette,
                "Roulette",
                moduleName: "Roulette",
                isCore: false,
                description: "Roulette game to try and roll a winning number and win points. Can have daily limits and cooldowns."
            );
            services.AddRuntimeFeatureService<DuelGame>(
                FeatureKeys.DuelGame,
                "Duel",
                moduleName: "Duel",
                isCore: false,
                description: "Duel game to challenge other viewers and win points."
            );
            services.AddRuntimeFeatureService<ModSpam>(
                FeatureKeys.ModSpam,
                "Mod Spam",
                moduleName: "ModSpam",
                isCore: false,
                description: "Moderator spam command that silently spams add points to all active viewers."
            );
            services.AddRuntimeFeatureService<AddActive>(
                FeatureKeys.AddActive,
                "Add Active",
                moduleName: "AddActive",
                isCore: false,
                description: "Adds points to all active viewers."
            );
            services.AddRuntimeFeatureService<First>(
                FeatureKeys.First,
                "First",
                moduleName: "First",
                isCore: false,
                description: "First users to use command in chat to get extra points based on their ranking of using it since stream went live."
            );
            services.AddRuntimeFeatureService<Bot.Commands.Misc.DailyCounter>(
                FeatureKeys.DailyCounter,
                "Daily Counter",
                moduleName: "DailyCounter",
                isCore: false,
                description: "Tracks daily messages and subscriptions."
            );
            services.AddRuntimeFeatureService<Bot.Commands.Misc.DeathCounters>(
                FeatureKeys.DeathCounter,
                "Death Counter",
                moduleName: "DeathCounter",
                isCore: false,
                description: "Tracks viewer deaths in games. Each game has its own death counter and can be reset by the streamer."
            );
            services.AddRuntimeFeatureService<Bot.Commands.Misc.LastSeen>(
                FeatureKeys.LastSeen,
                "Last Seen",
                moduleName: "LastSeen",
                isCore: false,
                description: "Last seen viewer activity tracking."
            );
            services.AddRuntimeFeatureService<Top>(
                FeatureKeys.Top,
                "Top",
                moduleName: "Top",
                isCore: false,
                description: "Top commands for game and loyalty leaderboards."
            );
            services.AddRuntimeFeatureService<Bot.Commands.Misc.QuoteSystem>(
                FeatureKeys.QuoteSystem,
                "Quotes",
                isCore: false,
                description: "Quote commands and viewer quotes page."
                );
            services.AddHostedApiService<Bot.Commands.Misc.RaidTracker>();
            services.AddRuntimeFeatureService<Bot.Commands.Misc.Weather>(
                FeatureKeys.Weather,
                "Weather",
                moduleName: "Weather",
                isCore: false,
                description: "Weather lookup command."
            );
            services.AddHostedApiService<Bot.Commands.Misc.ShoutoutSystem>();
            services.AddRuntimeFeatureService<IAutoTimers, AutoTimers>(
                FeatureKeys.AutoTimer,
                "Auto Timer",
                moduleName: "Timers",
                isCore: false,
                description: "Automatic timer feature for scheduled events."
            );
            services.AddHostedApiService<AudioCommands>();
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Defuse>(
                FeatureKeys.Defuse,
                "Defuse",
                moduleName: "Defuse",
                isCore: false,
                description: "Defuse the bomb game."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Roll>(
                FeatureKeys.Roll,
                "Roll",
                moduleName: "Roll",
                isCore: false,
                description: "Roll the dice game."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.FFA>(
                FeatureKeys.FFA,
                "FFA",
                moduleName: "FFA",
                isCore: false,
                description: "Free-for-all game."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Gamble>(
                FeatureKeys.Gamble,
                "Gamble",
                moduleName: "Gamble",
                isCore: false,
                description: "Gamble points game with a progressive jackpot."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Steal>(
                FeatureKeys.Steal,
                "Steal",
                moduleName: "Steal",
                isCore: false,
                description: "Steal points from other viewers game."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Heist>(
                FeatureKeys.Heist,
                "Heist",
                moduleName: "Heist",
                isCore: false,
                description: "Heist points game."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Slots>(
                FeatureKeys.Slots,
                "Slots",
                moduleName: "Slots",
                isCore: false,
                description: "Slots game."
            );
            services.AddRuntimeFeatureService<Bot.Commands.PastyGames.Tax>(
                FeatureKeys.Tax,
                "Tax",
                moduleName: "Tax",
                isCore: false,
                description: "Passive tax timer that runs after stream end."
            );
            services.AddRuntimeFeatureService<Bot.Commands.Music.YtPlayer>(
                FeatureKeys.MusicPlayer,
                "Music Player",
                isCore: false,
                description: "YouTube music player, requests queue, and playback controls."
            );
            services.AddHostedApiService<Bot.Commands.Moderation.Blacklist>();
            services.AddHostedApiService<Bot.Commands.Moderation.Admin>();
            services.AddHostedApiService<Bot.Commands.Metrics.SongRequests>();
            services.AddHostedApiService<Bot.Commands.Moderation.BannedUsers>();
            services.AddRuntimeFeatureService<PenguinTwitchBot.Bot.Core.IDiscordService, PenguinTwitchBot.Bot.Core.DiscordService>(
                FeatureKeys.DiscordService,
                "Discord",
                isCore: false,
                description: "Discord service integration for bot notifications and commands."
            );
            services.AddRuntimeFeatureService<IVersionCheckService, VersionCheckService>(
                FeatureKeys.VersionCheck,
                "Version Check",
                isCore: false,
                description: "Checks for new bot releases on startup and periodically."
            );
            services.AddRuntimeFeatureService<Bot.ScheduledJobs.FishingTournamentScheduler>(
                FeatureKeys.Fishing,
                "Fishing",
                isCore: false,
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards."
            );

            services.AddRuntimeFeatureService<ITTSService, TTSService>(
                FeatureKeys.TTS,
                "Text-to-Speech",
                moduleName: "TTSService",
                isCore: false,
                description: "Text-to-speech service for reading messages aloud."
            );
            services.AddRuntimeFeatureService<IClipService, ClipService>(
                FeatureKeys.ClipService,
                "Clip Service",
                isCore: false,
                description: "Clip service for creating and managing Twitch clips."
            );
            services.AddRuntimeFeatureService<IWheelService, WheelService>(
                FeatureKeys.WheeledGame,
                "Wheeled Game",
                moduleName: "WheelService",
                isCore: false,
                description: "Wheeled game service for managing wheel game interactions and rewards."
            );
            services.AddRuntimeFeatureService<Bot.Core.Points.IPointsSystem, Bot.Core.Points.PointsSystem>(
                FeatureKeys.PointsSystem,
                "Points",
                isCore: true,
                description: "Core points system used by commands, pages, and feature integrations.");
            services.AddRuntimeFeatureService<Bot.Core.Points.ITwitchEventsBonus, Bot.Core.Points.TwitchEventsBonus>(
                FeatureKeys.TwitchEventsBonus,
                "Twitch Events Bonus",
                isCore: false,
                description: "Twitch events bonus points system for channel point redemptions and event tracking."
            );

            // Fishing services - core service and specialized services
            services.AddSingleton<IFishingService, FishingService>();
            services.AddSingleton<IFishingShopService, FishingShopService>();
            services.AddSingleton<IFishingInventoryService, FishingInventoryService>();
            services.AddSingleton<IFishingGameplayService, FishingGameplayService>();
            services.AddSingleton<IFishingAnalyticsService, FishingAnalyticsService>();
            services.AddSingleton<IFishingLeaderboardService, FishingLeaderboardService>();
            services.AddSingleton<IFishingHelpDataService, FishingHelpDataService>();

            services.AddRuntimeFeatureRegistration<IFishingService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards."
                );
            services.AddRuntimeFeatureRegistration<IFishingShopService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards.");
            services.AddRuntimeFeatureRegistration<IFishingInventoryService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards.");
            services.AddRuntimeFeatureRegistration<IFishingGameplayService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards.");
            services.AddRuntimeFeatureRegistration<IFishingAnalyticsService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards.");
            services.AddRuntimeFeatureRegistration<IFishingLeaderboardService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards.");
            services.AddRuntimeFeatureRegistration<IFishingHelpDataService>(
                FeatureKeys.Fishing,
                "Fishing",
                description: "Fishing services, shop, inventory, gameplay, analytics, and leaderboards.");

            services.AddHostedApiService<ScAi>();

            services.AddSingleton<Bot.Commands.ICommandHelper, Bot.Commands.CommandHelper>();
            services.AddSingleton<ITTSPlayerService, TTSPlayerService>();
            services.AddSingleton<IChatMessageIdTracker, ChatMessageIdTracker>();
            services.AddSingleton<IServiceMaintenance, ServiceMaintenance>();

            services.AddRuntimeFeatureService<IChatHistory, ChatHistory>(
                FeatureKeys.ChatHistory,
                "Chat History",
                isCore: false,
                description: "Chat history service for storing and retrieving logged chat messages."
            );

            services.AddSingleton<Bot.Core.Leaderboards>();
            services.AddScoped<Bot.Commands.ChannelPoints.IChannelPoints, Bot.Commands.ChannelPoints.ChannelPoints>();
            services.AddSingleton<IGameSettingsService, GameSettingsService>();
            services.AddSingleton<ITools, Tools>();
            //services.AddSingleton<ITimer, Timer>();

            services.AddScoped<Bot.Actions.IAction, Bot.Actions.Action>();

            // Register SubAction handlers automatically
            services.AddSubActionHandlers();

            // Register Action Execution Logger
            services.AddSingleton<Bot.Queues.IActionExecutionLogger, Bot.Queues.ActionExecutionLogger>();

            // Global concurrency limiter � shared SemaphoreSlim across all non-blocking queues
            services.AddSingleton<Bot.Queues.GlobalConcurrencyLimiter>();

            // Register Queue Manager
            services.AddRuntimeFeatureService<Bot.Queues.IQueueManager, Bot.Queues.QueueManager>(
                FeatureKeys.QueueManager,
                "Queue Manager",
                isCore: true,
                description: "Manages action queues for the bot."
            );
            services.AddTransient<Bot.Hubs.ISignalRHubConnectionFactory, Bot.Hubs.SignalRHubConnectionFactory>();
            services.AddHostedApiService<RuntimeHealthSnapshotService>();

            // Register Validation Service (Singleton for cross-scope result caching)
            services.AddSingleton<Bot.Validation.IValidationService, Bot.Validation.ValidationService>();
            services.AddSingleton<FeatureRuntimeCoordinator>();
            services.AddSingleton<IFeatureRuntimeCoordinator>(p => p.GetRequiredService<FeatureRuntimeCoordinator>());
            services.AddSingleton<IHostedService>(p => p.GetRequiredService<FeatureRuntimeCoordinator>());

            return services;
        }

        public static void AddHostedApiService<TService>(this IServiceCollection services) where TService : class, IHostedService
        {
            services.AddSingleton<TService>();
            services.AddSingleton<IHostedService>(p => (TService)p.GetRequiredService<TService>());
        }

        public static void AddHostedApiService<TInterface, TService>(this IServiceCollection services)
            where TInterface : class
            where TService : class, IHostedService, TInterface
        {
            services.AddSingleton<TInterface, TService>();
            services.AddSingleton<IHostedService>(p => (TService)p.GetRequiredService<TInterface>());
        }

        public static void AddRuntimeFeatureService<TService>(
            this IServiceCollection services,
            string featureKey,
            string displayName,
            bool isCore,
            string description = "")
            where TService : class, IHostedService
        {
            services.AddSingleton<TService>();
            services.AddSingleton(new RuntimeFeatureRegistration(featureKey, displayName, featureKey, typeof(TService), isCore, description));
        }

        public static void AddRuntimeFeatureService<TService>(
            this IServiceCollection services,
            string featureKey,
            string displayName,
            string moduleName,
            bool isCore,
            string description = "")
            where TService : class, IHostedService
        {
            services.AddSingleton<TService>();
            services.AddSingleton(new RuntimeFeatureRegistration(featureKey, displayName, moduleName, typeof(TService), isCore, description));
        }

        public static void AddRuntimeFeatureService<TInterface, TService>(
            this IServiceCollection services,
            string featureKey,
            string displayName,
            bool isCore,
            string description = "")
            where TInterface : class
            where TService : class, IHostedService, TInterface
        {
            services.AddSingleton<TService>();
            services.AddSingleton<TInterface>(p => p.GetRequiredService<TService>());
            services.AddSingleton(new RuntimeFeatureRegistration(featureKey, displayName, featureKey, typeof(TService), isCore, description));
        }

        public static void AddRuntimeFeatureService<TInterface, TService>(
            this IServiceCollection services,
            string featureKey,
            string displayName,
            string moduleName,
            bool isCore,
            string description = "")
            where TInterface : class
            where TService : class, IHostedService, TInterface
        {
            services.AddSingleton<TService>();
            services.AddSingleton<TInterface>(p => p.GetRequiredService<TService>());
            services.AddSingleton(new RuntimeFeatureRegistration(featureKey, displayName, moduleName, typeof(TService), isCore, description));
        }

        public static void AddRuntimeFeatureRegistration<TService>(
            this IServiceCollection services,
            string featureKey,
            string displayName,
            bool isCore = false,
            string description = "")
            where TService : class
        {
            services.AddSingleton(new RuntimeFeatureRegistration(featureKey, displayName, featureKey, typeof(TService), isCore, description, false));
        }

        public static void AddRuntimeFeatureRegistration<TInterface, TService>(
            this IServiceCollection services,
            string featureKey,
            string displayName,
            bool isCore = false,
            string description = "")
            where TInterface : class
            where TService : class, TInterface
        {
            services.AddSingleton(new RuntimeFeatureRegistration(featureKey, displayName, featureKey, typeof(TService), isCore, description, false));
        }
    }
}
