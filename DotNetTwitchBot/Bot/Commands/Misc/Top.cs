using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class Top : BaseCommand
    {
        private IServiceScopeFactory _scopeFactory;

        public Top(
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone
            ) : base(serviceBackbone)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testtop10":
                    await SayTopN(10);
                    break;
                case "testtop5":
                    await SayTopN(5);
                    break;
                case "testtoptime":
                    break;
            }
        }

        private async Task SayTopN(int topN)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var broadcasterName = _serviceBackbone.BroadcasterName;
                var botName = _serviceBackbone.BotName == null ? "" : _serviceBackbone.BotName;
                var top = db.ViewerPointWithRanks.Where(x => !broadcasterName.Equals(x.Username) && !botName.Equals(x.Username)).OrderBy(x => x.Ranking).Take(topN).ToList();
                var rank = 1;
                var names = string.Join(", ", top.Select(x => (rank++).ToString() + ". " + x.Username + " " + x.Points.ToString("N0")));
                await _serviceBackbone.SendChatMessage(string.Format("Top {0} in Pasties: {1}", topN, names));
            }
        }
    }
}