using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class Top : BaseCommandService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<Top> _logger;

        public Top(
            ILogger<Top> logger,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "Top";
            //Add so alias
            await RegisterDefaultCommand("top10", this, moduleName);
            await RegisterDefaultCommand("top5", this, moduleName);
            await RegisterDefaultCommand("toptime", this, moduleName);
            await RegisterDefaultCommand("toptickets", this, moduleName);
            //await RegisterDefaultCommand("topticket", this, moduleName); Add alias
            await RegisterDefaultCommand("loudest", this, moduleName);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "top10":
                    await SayPointsTopN(10);
                    break;
                case "top5":
                    await SayPointsTopN(5);
                    break;
                case "toptime":
                    await SayTimeTopN(5);
                    break;
                case "toptickets":
                    await SayTicketsTopN(10);
                    break;
                case "loudest":
                    await SayLoudestTopN(10);
                    break;
            }
        }

        private async Task SayLoudestTopN(int topN)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyFeature>();
            var top = await loyalty.GetTopNLoudest(topN);
            var names = string.Join(", ", top.Select(x => x.Ranking.ToString() + ". " + x.Username + " " + x.MessageCount.ToString("N0")));
            await ServiceBackbone.SendChatMessage(string.Format("Top {0} Loudest: {1}", topN, names));
        }

        private async Task SayPointsTopN(int topN)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var broadcasterName = ServiceBackbone.BroadcasterName;
            var botName = ServiceBackbone.BotName ?? "";
            var top = await db.ViewerPointWithRanks.GetAsync(filter: x => !broadcasterName.Equals(x.Username) && !botName.Equals(x.Username), orderBy: y => y.OrderBy(z => z.Ranking), limit: topN);
            var rank = 1;
            var names = string.Join(", ", top.Select(x => (rank++).ToString() + ". " + x.Username + " " + x.Points.ToString("N0")));
            await ServiceBackbone.SendChatMessage(string.Format("Top {0} in Pasties: {1}", topN, names));
        }

        private async Task SayTimeTopN(int topN)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var broadcasterName = ServiceBackbone.BroadcasterName;
            var botName = ServiceBackbone.BotName ?? "";
            var top = await db.ViewersTimeWithRank.GetAsync(filter: x => !broadcasterName.Equals(x.Username) && !botName.Equals(x.Username), orderBy: y => y.OrderBy(z => z.Ranking), limit: topN);
            var rank = 1;
            var names = string.Join(", ", top.Select(x => (rank++).ToString() + ". " + x.Username + " " + Tools.ConvertToCompoundDuration(x.Time)));
            await ServiceBackbone.SendChatMessage(string.Format("Top {0} in Time: {1}", topN, names));
        }

        private async Task SayTicketsTopN(int topN)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var broadcasterName = ServiceBackbone.BroadcasterName;
            var botName = ServiceBackbone.BotName ?? "";
            var top = await db.ViewerTicketsWithRank.GetAsync(x => !broadcasterName.Equals(x.Username) && !botName.Equals(x.Username), orderBy: y => y.OrderBy(z => z.Ranking), limit: topN);
            var rank = 1;
            var names = string.Join(", ", top.Select(x => (rank++).ToString() + ". " + x.Username + " " + x.Points.ToString("N0")));
            await ServiceBackbone.SendChatMessage(string.Format("Top {0} in Tickets: {1}", topN, names));
        }
    }
}