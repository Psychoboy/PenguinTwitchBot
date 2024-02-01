using DotNetTwitchBot.BackgroundWorkers;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Commands.TicketGames;
using DotNetTwitchBot.Bot.Commands.TTS;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.CustomMiddleware
{
    public static class BotCommandsRegistry
    {
        public static IServiceCollection AddBotCommands(this IServiceCollection services)
        {
            services.AddSingleton<Bot.Alerts.ISendAlerts, Bot.Alerts.SendAlerts>();
            services.AddSingleton<Bot.Notifications.IWebSocketMessenger, Bot.Notifications.WebSocketMessenger>();

            services.AddHostedApiService<Bot.Commands.Moderation.IKnownBots, Bot.Commands.Moderation.KnownBots>();

            services.AddSingleton<Bot.Core.SubscriptionTracker>();

            services.AddScoped(typeof(Repository.IGenericRepository<>), typeof(Repository.Repositories.GenericRepository<>));
            services.AddScoped<Repository.IUnitOfWork, Repository.UnitOfWork>();

            //Add Features Here:

            services.AddSingleton<Bot.Commands.PastyGames.MaxBetCalculator>();
            services.AddSingleton<Bot.Commands.Custom.IAlias, Bot.Commands.Custom.Alias>();
            //Add Alerts
            services.AddSingleton<Bot.Alerts.AlertImage>();


            services.AddHostedApiService<GiveawayFeature>();
            services.AddHostedApiService<WaffleRaffle>();
            services.AddHostedApiService<PancakeRaffle>();
            services.AddHostedApiService<BaconRaffle>();
            services.AddHostedApiService<Roulette>();
            services.AddHostedApiService<DuelGame>();
            services.AddHostedApiService<ModSpam>();
            services.AddHostedApiService<Bot.Commands.Misc.AddActive>();
            services.AddHostedApiService<Bot.Commands.Misc.First>();
            services.AddHostedApiService<Bot.Commands.Misc.DailyCounter>();
            services.AddHostedApiService<Bot.Commands.Misc.DeathCounters>();
            services.AddHostedApiService<Bot.Commands.Misc.LastSeen>();
            services.AddHostedApiService<Top>();
            services.AddHostedApiService<Bot.Commands.Misc.QuoteSystem>();
            services.AddHostedApiService<Bot.Commands.Misc.RaidTracker>();
            services.AddHostedApiService<Bot.Commands.Misc.Weather>();
            services.AddHostedApiService<Bot.Commands.Misc.ShoutoutSystem>();
            services.AddHostedApiService<Bot.Commands.Misc.Timers>();
            services.AddHostedApiService<Bot.Commands.Custom.CustomCommand>();
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
            services.AddHostedApiService<Bot.Commands.ChannelPoints.IChannelPointRedeem, Bot.Commands.ChannelPoints.ChannelPointRedeem>();
            services.AddHostedApiService<Bot.Commands.TwitchEvents.ITwitchEventsService, Bot.Commands.TwitchEvents.TwitchEventsService>();

            services.AddHostedApiService<ITTSService, TTSService>();


            RegisterCommandServices(services);
            services.AddSingleton<Bot.Commands.ICommandHelper, Bot.Commands.CommandHelper>();
            services.AddSingleton<ITTSPlayerService, TTSPlayerService>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton<ChatMessageIdTracker>();
            services.AddHostedService<BackgroundTaskService>();

            services.AddHostedApiService<IChatHistory, ChatHistory>();

            services.AddSingleton<Bot.Core.Leaderboards>();
            services.AddScoped<Bot.Commands.ChannelPoints.IChannelPoints, Bot.Commands.ChannelPoints.ChannelPoints>();
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
            services.AddHostedApiService<Bot.Commands.Features.ITicketsFeature, Bot.Commands.Features.TicketsFeature>();
            services.AddHostedApiService<Bot.Commands.Features.ILoyaltyFeature, Bot.Commands.Features.LoyaltyFeature>();
        }
    }
}
