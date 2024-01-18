using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Commands.TicketGames;

namespace DotNetTwitchBot.CustomMiddleware
{
    public static class BotCommandsRegistry
    {
        public static IServiceCollection AddBotCommands(this IServiceCollection services)
        {
            services.AddSingleton<Bot.Alerts.ISendAlerts, Bot.Alerts.SendAlerts>();
            services.AddSingleton<Bot.Notifications.IWebSocketMessenger, Bot.Notifications.WebSocketMessenger>();

            services.AddSingleton<Bot.Commands.Moderation.IKnownBots, Bot.Commands.Moderation.KnownBots>();
            services.AddHostedService<Bot.Commands.Moderation.KnownBots>();

            services.AddSingleton<Bot.Core.SubscriptionTracker>();

            services.AddScoped(typeof(Repository.IGenericRepository<>), typeof(Repository.Repositories.GenericRepository<>));
            services.AddScoped<Repository.IUnitOfWork, Repository.UnitOfWork>();

            //Add Features Here:

            services.AddSingleton<Bot.Commands.PastyGames.MaxBetCalculator>();
            services.AddSingleton<Bot.Commands.Custom.IAlias, Bot.Commands.Custom.Alias>();
            //Add Alerts
            services.AddSingleton<Bot.Alerts.AlertImage>();


            AddService<GiveawayFeature>(services);
            AddService<WaffleRaffle>(services);
            AddService<PancakeRaffle>(services);
            AddService<BaconRaffle>(services);
            AddService<Roulette>(services);
            AddService<DuelGame>(services);
            AddService<ModSpam>(services);
            AddService<Bot.Commands.Misc.AddActive>(services);
            AddService<Bot.Commands.Misc.First>(services);
            AddService<Bot.Commands.Misc.DailyCounter>(services);
            AddService<Bot.Commands.Misc.DeathCounters>(services);
            AddService<Bot.Commands.Misc.LastSeen>(services);
            AddService<Top>(services);
            AddService<Bot.Commands.Misc.QuoteSystem>(services);
            AddService<Bot.Commands.Misc.RaidTracker>(services);
            AddService<Bot.Commands.Misc.Weather>(services);
            AddService<Bot.Commands.Misc.ShoutoutSystem>(services);
            AddService<Bot.Commands.Misc.Timers>(services);
            AddService<Bot.Commands.Custom.CustomCommand>(services);
            AddService<AudioCommands>(services);
            AddService<Bot.Commands.PastyGames.Defuse>(services);
            AddService<Bot.Commands.PastyGames.Roll>(services);
            AddService<Bot.Commands.PastyGames.FFA>(services);
            AddService<Bot.Commands.PastyGames.Gamble>(services);
            AddService<Bot.Commands.PastyGames.Steal>(services);
            AddService<Bot.Commands.PastyGames.Heist>(services);
            AddService<Bot.Commands.PastyGames.Slots>(services);
            AddService<Bot.Commands.PastyGames.Tax>(services);
            AddService<Bot.Commands.Music.YtPlayer>(services);
            AddService<Bot.Commands.Moderation.Blacklist>(services);
            AddService<Bot.Commands.Moderation.Admin>(services);
            AddService<Bot.Commands.Metrics.SongRequests>(services);
            AddService<Bot.Commands.Moderation.BannedUsers>(services);


            RegisterCommandServices(services);
            services.AddSingleton<Bot.Commands.ICommandHelper, Bot.Commands.CommandHelper>();

            services.AddSingleton<Bot.Core.IChatHistory, Bot.Core.ChatHistory>();
            services.AddHostedService<Bot.Core.ChatHistory>();

            services.AddSingleton<Bot.Core.Leaderboards>();
            return services;
        }

        private static void AddService(Type service, IServiceCollection services)
        {
            services.AddSingleton(service);
        }

        private static void AddService<T>(IServiceCollection services) where T : IHostedService
        {
            AddService(typeof(T), services);
            services.AddHostedService<BackgroundServiceStarter<T>>();
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
