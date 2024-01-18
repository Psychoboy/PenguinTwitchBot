namespace DotNetTwitchBot.CustomMiddleware
{
    public static class BotCommandsRegistry
    {
        private static readonly List<Type> Commands =
        [
            typeof(Bot.Commands.Features.GiveawayFeature),
            typeof(Bot.Commands.TicketGames.WaffleRaffle),
            typeof(Bot.Commands.TicketGames.PancakeRaffle),
            typeof(Bot.Commands.TicketGames.BaconRaffle),
            typeof(Bot.Commands.TicketGames.Roulette),
            typeof(Bot.Commands.TicketGames.DuelGame),
            typeof(Bot.Commands.TicketGames.ModSpam),
            typeof(Bot.Commands.Misc.AddActive),
            typeof(Bot.Commands.Misc.First),
            typeof(Bot.Commands.Misc.DailyCounter),
            typeof(Bot.Commands.Misc.DeathCounters),
            typeof(Bot.Commands.Misc.LastSeen),
            typeof(Bot.Commands.Misc.Top),
            typeof(Bot.Commands.Misc.QuoteSystem),
            typeof(Bot.Commands.Misc.RaidTracker),
            typeof(Bot.Commands.Misc.Weather),
            typeof(Bot.Commands.Misc.ShoutoutSystem),
            typeof(Bot.Commands.Misc.Timers),
            typeof(Bot.Commands.Custom.CustomCommand),
            typeof(Bot.Commands.Custom.AudioCommands),
            typeof(Bot.Commands.PastyGames.Defuse),
            typeof(Bot.Commands.PastyGames.Roll),
            typeof(Bot.Commands.PastyGames.FFA),
            typeof(Bot.Commands.PastyGames.Gamble),
            typeof(Bot.Commands.PastyGames.Steal),
            typeof(Bot.Commands.PastyGames.Heist),
            typeof(Bot.Commands.PastyGames.Slots),
            typeof(Bot.Commands.PastyGames.Tax),
            typeof(Bot.Commands.Music.YtPlayer),
            typeof(Bot.Commands.Moderation.Blacklist),
            typeof(Bot.Commands.Moderation.Admin),
            typeof(Bot.Commands.Metrics.SongRequests),
            typeof(Bot.Commands.Moderation.BannedUsers)
        ];
        public static IServiceCollection AddBotCommands(this IServiceCollection services)
        {
            services.AddSingleton<Bot.Alerts.ISendAlerts, Bot.Alerts.SendAlerts>();
            services.AddSingleton<Bot.Notifications.IWebSocketMessenger, Bot.Notifications.WebSocketMessenger>();
            services.AddSingleton<Bot.Commands.Moderation.IKnownBots, Bot.Commands.Moderation.KnownBots>();
            services.AddSingleton<Bot.Core.SubscriptionTracker>();

            services.AddScoped(typeof(Repository.IGenericRepository<>), typeof(Repository.Repositories.GenericRepository<>));
            services.AddScoped<Repository.IUnitOfWork, Repository.UnitOfWork>();

            //Add Features Here:

            services.AddSingleton<Bot.Commands.PastyGames.MaxBetCalculator>();
            services.AddSingleton<Bot.Commands.Custom.IAlias, Bot.Commands.Custom.Alias>();
            //Add Alerts
            services.AddSingleton<Bot.Alerts.AlertImage>();

            foreach (var cmd in Commands)
            {
                services.AddSingleton(cmd);
            }

            RegisterCommandServices(services);
            services.AddSingleton<Bot.Commands.ICommandHelper, Bot.Commands.CommandHelper>();
            services.AddSingleton<Bot.Core.IChatHistory, Bot.Core.ChatHistory>();
            services.AddSingleton<Bot.Core.Leaderboards>();
            return services;
        }

        public static IApplicationBuilder RegisterBotCommands(this IApplicationBuilder app)
        {
            var task = RegisterCommands(app.ApplicationServices);
            task.Wait();
            app.ApplicationServices.GetService<Bot.Core.IChatHistory>(); //refactor having to do this in the future
            return app;
        }

        private static async Task RegisterCommands(IServiceProvider services)
        {
            await services.GetRequiredService<Bot.Commands.Moderation.IKnownBots>().LoadKnownBots();

            foreach (var cmd in Commands)
            {
                var commandService = (Bot.Commands.IBaseCommandService)services.GetRequiredService(cmd);
                await commandService.Register();
            }

            await ((Bot.Commands.IBaseCommandService)services.GetRequiredService<Bot.Commands.Features.IViewerFeature>()).Register();
            await ((Bot.Commands.IBaseCommandService)services.GetRequiredService<Bot.Commands.Features.ITicketsFeature>()).Register();
            await ((Bot.Commands.IBaseCommandService)services.GetRequiredService<Bot.Commands.Features.ILoyaltyFeature>()).Register();
        }

        private static void RegisterCommandServices(IServiceCollection services)
        {
            services.AddSingleton<Bot.Commands.Features.IViewerFeature, Bot.Commands.Features.ViewerFeature>();
            services.AddSingleton<Bot.Commands.Features.ITicketsFeature, Bot.Commands.Features.TicketsFeature>();
            services.AddSingleton<Bot.Commands.Features.ILoyaltyFeature, Bot.Commands.Features.LoyaltyFeature>();
        }
    }
}
