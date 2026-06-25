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
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client;

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

            services.AddTransient<WebsocketClient>();
            services.AddSingleton(x => new TwitchApi.EventSub.Websockets.EventSubWebsocketClient(x.GetRequiredService<ILogger<TwitchApi.EventSub.Websockets.EventSubWebsocketClient>>(), x.GetRequiredService<IServiceProvider>(), x.GetRequiredService<WebsocketClient>()));

            
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
            services.AddHostedApiService<ITwitchChatBot, TwitchChatBot>();
            services.AddHostedApiService<ITwitchWebsocketHostedService, TwitchWebsocketHostedService>();

            // OBS WebSocket Services
            services.AddSingleton<IOBSConnectionManager, OBSConnectionManager>();
            services.AddHostedService<OBSConnectionHostedService>();

            services.AddSingleton<Bot.Notifications.IWebSocketMessenger, Bot.Notifications.WebSocketMessenger>();
            services.AddSingleton<Bot.WebSocketEvents.IWsEventHandler, Bot.WebSocketEvents.WsEventHandler>();

            services.AddHostedApiService<Bot.Commands.Moderation.IKnownBots, Bot.Commands.Moderation.KnownBots>();

            services.AddSingleton<Bot.Core.SubscriptionTracker>();
            // IpLog is registered in Program.cs (always) so it's available even in setup mode.

            services.AddScoped(typeof(PenguinTwitchBot.Database.Repository.IGenericRepository<>), typeof(PenguinTwitchBot.Database.Repository.Repositories.GenericRepository<>));
            services.AddScoped<PenguinTwitchBot.Database.Repository.IUnitOfWork, PenguinTwitchBot.Database.Repository.UnitOfWork>();
            services.AddScoped<Bot.Actions.IActionManagementService, Bot.Actions.ActionManagementService>();
            services.AddScoped<Bot.Actions.IRaffleSetupService, Bot.Actions.RaffleSetupService>();
            services.AddScoped<Bot.Commands.IActionCommandService, Bot.Commands.ActionCommandService>();
            services.AddScoped<Bot.Commands.IActionKeywordService, Bot.Commands.ActionKeywordService>();
            services.AddSingleton<Bot.Commands.Actions.IActionKeywordCache, Bot.Commands.Actions.ActionKeywordCache>();
            services.AddScoped<ILurkBait, LurkBait>();
            services.AddScoped<IIpLogFeature, IpLogFeature>();
            //Add Features Here:

            services.AddSingleton<Bot.Commands.PastyGames.MaxBetCalculator>();
            services.AddSingleton<IAlias, Alias>();
            //Add Alerts
            services.AddSingleton<Bot.Alerts.AlertImage>();

            services.AddSingleton<IBonusTickets, BonusTickets>();
            services.AddSingleton<IRaffleRuntimeService, RaffleRuntimeService>();


            services.AddHostedApiService<GiveawayFeature>();
            services.AddHostedApiService<Roulette>();
            services.AddHostedApiService<DuelGame>();
            services.AddHostedApiService<ModSpam>();
            services.AddHostedApiService<AddActive>();
            services.AddHostedApiService<First>();
            services.AddHostedApiService<Bot.Commands.Misc.DailyCounter>();
            services.AddHostedApiService<Bot.Commands.Misc.DeathCounters>();
            services.AddHostedApiService<Bot.Commands.Misc.LastSeen>();
            services.AddHostedApiService<Top>();
            services.AddHostedApiService<Bot.Commands.Misc.QuoteSystem>();
            services.AddHostedApiService<Bot.Commands.Misc.RaidTracker>();
            services.AddHostedApiService<Bot.Commands.Misc.Weather>();
            services.AddHostedApiService<Bot.Commands.Misc.ShoutoutSystem>();
            services.AddHostedApiService<Bot.Commands.Misc.AutoTimers>();
            services.AddHostedApiService<AudioCommands>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Defuse>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Roll>();
            services.AddHostedApiService<Bot.Commands.PastyGames.FFA>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Gamble>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Steal>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Heist>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Slots>();
            services.AddHostedApiService<Bot.Commands.PastyGames.Tax>();
            services.AddHostedApiService<Bot.Commands.Music.YtPlayer>();
            services.AddHostedApiService<Bot.Commands.Moderation.Blacklist>();
            services.AddHostedApiService<Bot.Commands.Moderation.Admin>();
            services.AddHostedApiService<Bot.Commands.Metrics.SongRequests>();
            services.AddHostedApiService<Bot.Commands.Moderation.BannedUsers>();
            services.AddHostedApiService<Bot.Commands.Markov.IMarkovChat, Bot.Commands.Markov.MarkovChat>();
            services.AddHostedApiService<PenguinTwitchBot.Bot.Core.IDiscordService, PenguinTwitchBot.Bot.Core.DiscordService>();

            services.AddHostedApiService<ITTSService, TTSService>();
            services.AddHostedApiService<IClipService, ClipService>();
            services.AddHostedApiService<IWheelService, WheelService>();
            services.AddHostedApiService<Bot.Core.Points.IPointsSystem, Bot.Core.Points.PointsSystem>();
            services.AddHostedApiService<Bot.Core.Points.ITwitchEventsBonus, Bot.Core.Points.TwitchEventsBonus>();

            // Fishing services - core service and specialized services
            services.AddSingleton<IFishingService, FishingService>();
            services.AddSingleton<IFishingShopService, FishingShopService>();
            services.AddSingleton<IFishingInventoryService, FishingInventoryService>();
            services.AddSingleton<IFishingGameplayService, FishingGameplayService>();
            services.AddSingleton<IFishingAnalyticsService, FishingAnalyticsService>();
            services.AddSingleton<IFishingLeaderboardService, FishingLeaderboardService>();
            services.AddSingleton<IFishingHelpDataService, FishingHelpDataService>();

            services.AddHostedApiService<ScAi>();

            RegisterCommandServices(services);
            services.AddSingleton<Bot.Commands.ICommandHelper, Bot.Commands.CommandHelper>();
            services.AddSingleton<ITTSPlayerService, TTSPlayerService>();
            services.AddSingleton<ChatMessageIdTracker>();
            services.AddSingleton<IServiceMaintenance, ServiceMaintenance>();

            services.AddHostedApiService<IChatHistory, ChatHistory>();

            services.AddSingleton<Bot.Core.Leaderboards>();
            services.AddScoped<Bot.Commands.ChannelPoints.IChannelPoints, Bot.Commands.ChannelPoints.ChannelPoints>();
            services.AddSingleton<IGameSettingsService, GameSettingsService>();
            services.AddSingleton<ITools, Tools>();
            //services.AddSingleton<ITimer, Timer>();

            services.AddSingleton<Bot.Markov.TokenisationStrategies.StringMarkov>();

            services.AddScoped<Bot.Actions.IAction, Bot.Actions.Action>();

            // Register SubAction handlers automatically
            services.AddSubActionHandlers();

            // Register Action Execution Logger
            services.AddSingleton<Bot.Queues.IActionExecutionLogger, Bot.Queues.ActionExecutionLogger>();

            // Global concurrency limiter � shared SemaphoreSlim across all non-blocking queues
            services.AddSingleton<Bot.Queues.GlobalConcurrencyLimiter>();

            // Register Queue Manager
            services.AddHostedApiService<Bot.Queues.IQueueManager, Bot.Queues.QueueManager>();
            services.AddHostedApiService<RuntimeHealthSnapshotService>();

            // Register Validation Service (Singleton for cross-scope result caching)
            services.AddSingleton<Bot.Validation.IValidationService, Bot.Validation.ValidationService>();

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

        private static void RegisterCommandServices(IServiceCollection services)
        {
            services.AddHostedApiService<Bot.Commands.Features.IViewerFeature, Bot.Commands.Features.ViewerFeature>();
            services.AddHostedApiService<Bot.Commands.Features.ILoyaltyFeature, Bot.Commands.Features.LoyaltyFeature>();
        }
    }
}
